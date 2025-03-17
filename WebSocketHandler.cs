using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ContigoServer
{
    public class WebSocketHandler
    {
        private static ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();
        private static ConcurrentDictionary<string, PositionData> _playerPositions = new ConcurrentDictionary<string, PositionData>();
        private static ConcurrentDictionary<string, PositionData> _playerDesiredPositions = new ConcurrentDictionary<string, PositionData>();
        private const float FixedDeltaTime = 0.05f; // 20 Hz
        private bool _isHealthy = true;
        private readonly IWebHostEnvironment _environment;
        private static Random _rnd = new Random();

        public WebSocketHandler(IWebHostEnvironment environment)
        {
            _environment = environment;
            Console.WriteLine("[WebSocketHandler] Handler initialized.");
            CancellationTokenSource cts = new CancellationTokenSource();
            _ = PhysicsUpdateLoopAsync(cts.Token);
        }

        public async Task HandleHealthCheckAsync(HttpContext context)
        {
            Console.WriteLine("[HandleHealthCheckAsync] Health check requested.");
            if (_environment.IsDevelopment() || _isHealthy)
            {
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("OK");
            }
            else
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("Service Unavailable");
            }
        }

        public async Task HandleWebSocketAsync(HttpContext context, WebSocket socket)
        {
            string socketId = Guid.NewGuid().ToString();
            Console.WriteLine($"[HandleWebSocketAsync] New connection: {socketId}");

            if (!_sockets.TryAdd(socketId, socket))
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Duplicate connection", CancellationToken.None);
                return;
            }

            float spawnX = (float)(_rnd.NextDouble() * 10.0 - 5.0);
            float spawnZ = (float)(_rnd.NextDouble() * 10.0 - 5.0);
            var initialPos = new PositionData { X = spawnX, Y = 0, Z = spawnZ };
            _playerPositions.TryAdd(socketId, initialPos);
            _playerDesiredPositions.TryAdd(socketId, initialPos);

            _isHealthy = true;
            await SendIdToClient(socketId, socket);
            await Receive(socketId, socket);

            if (_sockets.TryRemove(socketId, out _) &&
                _playerPositions.TryRemove(socketId, out _) &&
                _playerDesiredPositions.TryRemove(socketId, out _))
            {
                Console.WriteLine($"[HandleWebSocketAsync] Cleaned up: {socketId}");
            }
            _isHealthy = _sockets.Count > 0;
        }

        private async Task SendIdToClient(string socketId, WebSocket socket)
        {
            var idMessage = new { type = "ID", id = socketId };
            string json = JsonConvert.SerializeObject(idMessage);
            if (socket.State == WebSocketState.Open)
            {
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                Console.WriteLine($"[SendIdToClient] Sent ID to {socketId}");
            }
        }

        private async Task Receive(string socketId, WebSocket socket)
        {
            var buffer = new byte[1024 * 4];
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ProcessInput(socketId, message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                        Disconnect(socketId);
                        await BroadcastSnapshotAsync(new Snapshot
                        {
                            Timestamp = DateTime.UtcNow.Ticks,
                            Positions = _playerPositions,
                            Velocities = new ConcurrentDictionary<string, Vector3Data>(),
                            Collisions = new ConcurrentDictionary<string, CollisionData>()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Receive] Error for {socketId}: {ex.Message}");
                Disconnect(socketId);
            }
        }

        private void ProcessInput(string socketId, string message)
        {
            try
            {
                var input = JsonConvert.DeserializeObject<InputMessage>(message);
                if (input != null)
                {
                    _playerDesiredPositions[socketId] = new PositionData
                    {
                        X = input.X,
                        Y = input.Y,
                        Z = input.Z
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessInput] Error for {socketId}: {ex.Message}");
            }
        }

        private async Task PhysicsUpdateLoopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[PhysicsUpdateLoopAsync] Starting physics loop.");
            float personalSpace = 0.7f;

            while (!cancellationToken.IsCancellationRequested)
            {
                var newPositions = new ConcurrentDictionary<string, PositionData>();
                var collisions = new ConcurrentDictionary<string, CollisionData>();

                // Update positions from desired positions
                foreach (var kvp in _playerDesiredPositions)
                {
                    string id = kvp.Key;
                    PositionData desired = kvp.Value;
                    newPositions[id] = new PositionData { X = desired.X, Y = desired.Y, Z = desired.Z };
                }

                // Detect collisions and calculate force directions
                foreach (var kvp in newPositions)
                {
                    string id_i = kvp.Key;
                    PositionData pos_i = kvp.Value;

                    foreach (var kvp2 in newPositions)
                    {
                        string id_j = kvp2.Key;
                        if (id_i == id_j) continue;
                        PositionData pos_j = kvp2.Value;

                        float dx = pos_i.X - pos_j.X;
                        float dz = pos_i.Z - pos_j.Z;
                        float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                        if (distance < personalSpace && distance > 0)
                        {
                            // Normalize direction from other player to this player
                            float norm_dx = dx / distance;
                            float norm_dz = dz / distance;

                            // Add collision data for both players
                            collisions[id_i] = new CollisionData
                            {
                                OtherPlayerId = id_j,
                                DirectionX = norm_dx,
                                DirectionZ = norm_dz
                            };
                            collisions[id_j] = new CollisionData
                            {
                                OtherPlayerId = id_i,
                                DirectionX = -norm_dx, // Opposite direction for the other player
                                DirectionZ = -norm_dz
                            };

                            // Apply basic separation (still authoritative)
                            float overlap = personalSpace - distance;
                            pos_i.X += norm_dx * overlap / 2;
                            pos_i.Z += norm_dz * overlap / 2;
                            pos_j.X -= norm_dx * overlap / 2;
                            pos_j.Z -= norm_dz * overlap / 2;
                        }
                    }
                }

                // Update authoritative positions
                foreach (var kvp in newPositions)
                {
                    _playerPositions[kvp.Key] = kvp.Value;
                }

                // Broadcast snapshot with collision data
                var snapshot = new Snapshot
                {
                    Timestamp = DateTime.UtcNow.Ticks,
                    Positions = _playerPositions,
                    Velocities = new ConcurrentDictionary<string, Vector3Data>(),
                    Collisions = collisions
                };
                await BroadcastSnapshotAsync(snapshot);

                await Task.Delay(TimeSpan.FromSeconds(FixedDeltaTime), cancellationToken);
            }
        }

        private async Task BroadcastSnapshotAsync(Snapshot snapshot)
        {
            if (_sockets.IsEmpty) return;

            string json = JsonConvert.SerializeObject(snapshot);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            foreach (var kvp in _sockets)
            {
                var socket = kvp.Value;
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

        private void Disconnect(string socketId)
        {
            if (_sockets.TryRemove(socketId, out _) &&
                _playerPositions.TryRemove(socketId, out _) &&
                _playerDesiredPositions.TryRemove(socketId, out _))
            {
                Console.WriteLine($"[Disconnect] Disconnected: {socketId}");
            }
        }
    }

    public class InputMessage
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class PositionData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class Vector3Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class CollisionData
    {
        public string OtherPlayerId { get; set; }
        public float DirectionX { get; set; }
        public float DirectionZ { get; set; }
    }

    public class Snapshot
    {
        public long Timestamp { get; set; }
        public ConcurrentDictionary<string, PositionData> Positions { get; set; }
        public ConcurrentDictionary<string, Vector3Data> Velocities { get; set; }
        public ConcurrentDictionary<string, CollisionData> Collisions { get; set; }
    }
}