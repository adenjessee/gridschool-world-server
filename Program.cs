using ContigoServer;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Enable WebSockets
app.UseWebSockets();

// Create a single instance of WebSocketHandler
var handler = new WebSocketHandler();
var cts = new CancellationTokenSource();
Task.Run(() => handler.StartBroadcastLoopAsync(cts.Token));
// Task.Run(() => handler.StartHeartbeatCheckAsync(cts.Token));

// Start the broadcast loop in the background
_ = Task.Run(() => handler.StartBroadcastLoopAsync(CancellationToken.None));

// Map the "/ws" endpoint to handle WebSocket connections
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        // Accept the WebSocket once here.
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        await handler.HandleWebSocketAsync(context, socket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Run();