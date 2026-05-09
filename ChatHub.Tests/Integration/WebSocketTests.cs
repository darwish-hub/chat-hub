using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace ChatHub.Tests.Integration;

public class WebSocketTests
{
    [Fact(Skip = "Requires running server - run manually or with WebApplicationFactory")]
    public async Task CanConnect_WithValidToken()
    {
        // This test would connect to the WebSocket endpoint
        // Requires a running server or WebApplicationFactory setup
    }

    [Fact(Skip = "Requires running server")]
    public async Task CanSendAndReceiveMessage()
    {
        // Arrange
        var client = new ClientWebSocket();
        var token = "valid-jwt-token"; // Would need proper token generation

        // Act
        await client.ConnectAsync(
            new Uri($"ws://localhost:5123/ws?token={token}"),
            CancellationToken.None);

        var message = new
        {
            type = "text_message",
            id = Guid.NewGuid().ToString(),
            conversationId = "test-conv",
            serviceId = "test-svc",
            text = "Hello, World!"
        };

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await client.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);

        // Assert
        var buffer = new byte[1024];
        var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);

        var response = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Assert.Contains("delivered", response);
    }
}
