using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ContigoServer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSingleton<WebSocketHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseWebSockets();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        var handler = app.Services.GetRequiredService<WebSocketHandler>();
        await handler.HandleWebSocketAsync(context, socket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.MapGet("/health", async context =>
{
    var handler = app.Services.GetRequiredService<WebSocketHandler>();
    await handler.HandleHealthCheckAsync(context);
});

app.Run();