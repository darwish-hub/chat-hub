using ChatHub.Core.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace ChatHub.Tests.Unit.Handlers;

public class MessageSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void TextMessage_ShouldSerializeCorrectly()
    {
        var message = new TextMessage
        {
            Id = "test-id",
            ConversationId = "conv-123",
            ServiceId = "svc-456",
            Text = "Hello, World!",
            ReplyToId = "msg-789"
        };

        var json = JsonSerializer.Serialize(message, JsonOptions);
        
        json.Should().Contain("\"type\":\"text_message\"");
        json.Should().Contain("\"id\":\"test-id\"");
        json.Should().Contain("\"conversationId\":\"conv-123\"");
        json.Should().Contain("\"text\":\"Hello, World!\"");
        json.Should().Contain("\"replyToId\":\"msg-789\"");
    }

    [Fact]
    public void TextMessage_ShouldDeserializeCorrectly()
    {
        var json = @"{
            ""type"": ""text_message"",
            ""id"": ""test-id"",
            ""conversationId"": ""conv-123"",
            ""serviceId"": ""svc-456"",
            ""text"": ""Hello, World!"",
            ""replyToId"": ""msg-789""
        }";

        var message = JsonSerializer.Deserialize<TextMessage>(json, JsonOptions);

        message.Should().NotBeNull();
        message!.Type.Should().Be("text_message");
        message.Id.Should().Be("test-id");
        message.ConversationId.Should().Be("conv-123");
        message.Text.Should().Be("Hello, World!");
    }

    [Fact]
    public void ServerMessage_ShouldSerializeCorrectly()
    {
        var message = new UserJoined
        {
            UserId = "user-123",
            ServiceId = "svc-456",
            DisplayName = "John Doe"
        };

        var json = JsonSerializer.Serialize(message, JsonOptions);

        json.Should().Contain("\"type\":\"user_joined\"");
        json.Should().Contain("\"userId\":\"user-123\"");
        json.Should().Contain("\"displayName\":\"John Doe\"");
    }
}
