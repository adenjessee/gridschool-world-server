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
        // Removed heartbeat dictionary and related timeouts for simplicity

        private const float Speed = 2.0f;
        private const float FixedDeltaTime = 0.05f;
        private const float SendInterval = 0.05f;
        // Removed HeartbeatTimeout constant

        public async Task HandleWebSocketAsync(HttpContext context, WebSocket socket)
        {
            string socketId = Guid.NewGuid().ToString();
            Console.WriteLine($"[HandleWebSocketAsync] New connection with socketId: {socketId}");
            _sockets.TryAdd(socketId, socket);
            _playerPositions.TryAdd(socketId, new PositionData { X = 0, Y = 0, Z = 0 });
            Console.WriteLine($"[HandleWebSocketAsync] Added socket and initialized position for: {socketId}");

            await SendIdToClient(socketId, socket);
            Console.WriteLine($"[HandleWebSocketAsync] Sent ID to client: {socketId}");
            await Receive(socketId, socket);
        }

        private async Task SendIdToClient(string socketId, WebSocket socket)
        {
            var idMessage = new { type = "ID", id = socketId };
            string json = JsonConvert.SerializeObject(idMessage);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            Console.WriteLine($"[SendIdToClient] Sending ID message: {json}");

            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                Console.WriteLine($"[SendIdToClient] Message sent to socketId: {socketId}");
            }
            else
            {
                Console.WriteLine($"[SendIdToClient] Socket not open for socketId: {socketId}");
            }
        }

        private async Task Receive(string socketId, WebSocket socket)
        {
            var buffer = new byte[1024 * 4];
            Console.WriteLine($"[Receive] Starting receive loop for socketId: {socketId}");
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string serializedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"[Receive] Received message from {socketId}: {serializedMessage}");
                        ProcessInput(socketId, serializedMessage);
                        // Removed heartbeat update
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine($"[Receive] Close message received from {socketId}");
                        Disconnect(socketId);
                        await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                        Console.WriteLine($"[Receive] Socket closed for {socketId}");
                        await BroadcastPositionsAsync(); // Immediate broadcast on disconnect
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Receive] Exception for socketId {socketId}: {ex.Message}");
                // Log the error but do not disconnect automatically.
            }
        }

        private const float MaxDelta = 0.5f;

        private void ProcessInput(string socketId, string message)
        {
            Console.WriteLine($"[ProcessInput] Processing message from {socketId}: {message}");
            try
            {
                var input = JsonConvert.DeserializeObject<InputMessage>(message);
                if (input == null)
                {
                    Console.WriteLine($"[ProcessInput] Deserialized input is null for {socketId}");
                    return;
                }

                if (_playerPositions.TryGetValue(socketId, out var pos))
                {
                    pos.X = input.X;
                    pos.Y = input.Y;
                    pos.Z = input.Z;
                    _playerPositions[socketId] = pos;
                    Console.WriteLine($"[ProcessInput] Updated position for {socketId}: ({pos.X}, {pos.Y}, {pos.Z})");
                }
                else
                {
                    Console.WriteLine($"[ProcessInput] No position data found for {socketId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessInput] Error processing input for {socketId}: {ex.Message}");
            }
        }
    
        public async Task BroadcastPositionsAsync()
        {
            string snapshot = JsonConvert.SerializeObject(_playerPositions);
            byte[] buffer = Encoding.UTF8.GetBytes(snapshot);
            Console.WriteLine($"[BroadcastPositionsAsync] Broadcasting snapshot with {_playerPositions.Count} players: {snapshot}");

            // List to store socket IDs that need to be disconnected (only used if a socket isn't open)
            List<string> socketsToDisconnect = new List<string>();

            foreach (var kvp in _sockets)
            {
                var socket = kvp.Value;
                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                        Console.WriteLine($"[BroadcastPositionsAsync] Broadcasted to socketId: {kvp.Key}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BroadcastPositionsAsync] Error broadcasting to {kvp.Key}: {ex.Message}");
                        // Do not disconnect on error automatically
                    }
                }
                else
                {
                    Console.WriteLine($"[BroadcastPositionsAsync] Socket {kvp.Key} is not open.");
                }
            }
        }

        public async Task StartBroadcastLoopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[StartBroadcastLoopAsync] Starting broadcast loop.");
            while (!cancellationToken.IsCancellationRequested)
            {
                await BroadcastPositionsAsync();
                await Task.Delay(TimeSpan.FromSeconds(SendInterval), cancellationToken);
            }
            Console.WriteLine("[StartBroadcastLoopAsync] Broadcast loop cancelled.");
        }

        // Removed StartHeartbeatCheckAsync to prevent auto-disconnection on heartbeat timeouts

        private void Disconnect(string socketId)
        {
            Console.WriteLine($"[Disconnect] Disconnecting socketId: {socketId}");
            _sockets.TryRemove(socketId, out _);
            _playerPositions.TryRemove(socketId, out _);
            // Removed heartbeat removal as it's no longer used
            Console.WriteLine($"[Disconnect] Disconnected socketId: {socketId}");
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
        public bool Initialized { get; set; } = false;
    }
}
