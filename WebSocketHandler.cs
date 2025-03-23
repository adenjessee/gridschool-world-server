using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Numerics; // For System.Numerics.Quaternion
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ContigoServer
{
    public class WebSocketHandler
    {
        private static ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();
        private static ConcurrentDictionary<string, PlayerData> _playerData = new ConcurrentDictionary<string, PlayerData>();
        private const float FixedDeltaTime = 0.05f; // 20 Hz
        private bool _isHealthy = true;
        private readonly IWebHostEnvironment _environment;
        private static Random _rnd = new Random();

        public WebSocketHandler(IWebHostEnvironment environment)
        {
            _environment = environment;
            Console.WriteLine("[WebSocketHandler] Handler initialized.");
            CancellationTokenSource cts = new CancellationTokenSource();
            _ = BroadcastLoopAsync(cts.Token);
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
            var initialData = new PlayerData
            {
                Position = new PositionData { X = spawnX, Y = 0, Z = spawnZ, Angle = 0f },
                Animation = new AnimationState { Speed = 0f, MotionSpeed = 0f, Jump = false, Grounded = true, FreeFall = false }
            };
            _playerData.TryAdd(socketId, initialData);

            _isHealthy = true;
            await SendIdToClient(socketId, socket);
            await Receive(socketId, socket);

            if (_sockets.TryRemove(socketId, out _) && _playerData.TryRemove(socketId, out _))
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
                            Positions = GetPositions(),
                            Rotations = ConvertToRotations(),
                            Animations = GetAnimations()
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
                    _playerData[socketId] = new PlayerData
                    {
                        Position = new PositionData
                        {
                            X = input.X,
                            Y = input.Y,
                            Z = input.Z,
                            Angle = input.Angle
                        },
                        Animation = new AnimationState
                        {
                            Speed = input.Speed,
                            MotionSpeed = input.MotionSpeed, // Added
                            Jump = input.Jump,
                            Grounded = input.Grounded,
                            FreeFall = input.FreeFall
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessInput] Error for {socketId}: {ex.Message}");
            }
        }

        private async Task BroadcastLoopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[BroadcastLoopAsync] Starting broadcast loop.");
            while (!cancellationToken.IsCancellationRequested)
            {
                var snapshot = new Snapshot
                {
                    Timestamp = DateTime.UtcNow.Ticks,
                    Positions = GetPositions(),
                    Rotations = ConvertToRotations(),
                    Animations = GetAnimations()
                };
                await BroadcastSnapshotAsync(snapshot);
                await Task.Delay(TimeSpan.FromSeconds(FixedDeltaTime), cancellationToken);
            }
        }

        private ConcurrentDictionary<string, PositionData> GetPositions()
        {
            var positions = new ConcurrentDictionary<string, PositionData>();
            foreach (var kvp in _playerData)
            {
                positions[kvp.Key] = kvp.Value.Position;
            }
            return positions;
        }

        private ConcurrentDictionary<string, RotationData> ConvertToRotations()
        {
            var rotations = new ConcurrentDictionary<string, RotationData>();
            foreach (var kvp in _playerData)
            {
                float angleInRadians = kvp.Value.Position.Angle * (float)(Math.PI / 180.0); // Convert degrees to radians
                Quaternion quaternion = Quaternion.CreateFromYawPitchRoll(angleInRadians, 0, 0); // Yaw only
                rotations[kvp.Key] = new RotationData
                {
                    X = quaternion.X,
                    Y = quaternion.Y,
                    Z = quaternion.Z,
                    W = quaternion.W
                };
            }
            return rotations;
        }

        private ConcurrentDictionary<string, AnimationState> GetAnimations()
        {
            var animations = new ConcurrentDictionary<string, AnimationState>();
            foreach (var kvp in _playerData)
            {
                animations[kvp.Key] = kvp.Value.Animation;
            }
            return animations;
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
            if (_sockets.TryRemove(socketId, out _) && _playerData.TryRemove(socketId, out _))
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
        public float Angle { get; set; }
        public float Speed { get; set; }
        public float MotionSpeed { get; set; } // Added
        public bool Jump { get; set; }
        public bool Grounded { get; set; }
        public bool FreeFall { get; set; }
    }

    public class PlayerData
    {
        public PositionData Position { get; set; }
        public AnimationState Animation { get; set; }
    }

    public class PositionData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Angle { get; set; }
    }

    public class RotationData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }
    }

    public class AnimationState
    {
        public float Speed { get; set; }
        public float MotionSpeed { get; set; } // Added
        public bool Jump { get; set; }
        public bool Grounded { get; set; }
        public bool FreeFall { get; set; }
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
        public ConcurrentDictionary<string, RotationData> Rotations { get; set; }
        public ConcurrentDictionary<string, Vector3Data> Velocities { get; set; }
        public ConcurrentDictionary<string, CollisionData> Collisions { get; set; }
        public ConcurrentDictionary<string, AnimationState> Animations { get; set; }
    }
}