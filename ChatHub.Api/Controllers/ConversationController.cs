using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Security.Claims;

namespace ChatHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConversationController : ControllerBase
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<ConversationController> _logger;

    public ConversationController(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        ILogger<ConversationController> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConversationDocument>>> GetMyConversations(
        CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var conversations = await _conversationRepository.GetByUserAsync(userId, ct);
        return Ok(conversations);
    }

    [HttpGet("{conversationId}")]
    public async Task<ActionResult<ConversationDocument>> GetConversation(
        string conversationId,
        CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, ct);
        if (conversation == null)
        {
            return NotFound();
        }

        if (!conversation.ParticipantIds.Contains(userId))
        {
            return Forbid();
        }

        return Ok(conversation);
    }

    [HttpPost]
    public async Task<ActionResult<ConversationDocument>> CreateConversation(
        [FromBody] CreateConversationRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Validate request
        if (string.IsNullOrEmpty(request.ServiceId))
        {
            return BadRequest(new { error = "ServiceId is required" });
        }

        if (request.ParticipantIds == null || request.ParticipantIds.Count == 0)
        {
            return BadRequest(new { error = "At least one participant is required" });
        }

        // Add current user if not already included
        var participantIds = request.ParticipantIds.ToList();
        if (!participantIds.Contains(userId))
        {
            participantIds.Add(userId);
        }

        var conversation = new ConversationDocument
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ServiceId = request.ServiceId,
            ParticipantIds = participantIds,
            Title = request.Title,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _conversationRepository.InsertAsync(conversation, ct);

        _logger.LogInformation("Conversation {ConversationId} created by user {UserId}",
            conversation.Id, userId);

        return CreatedAtAction(
            nameof(GetConversation),
            new { conversationId = conversation.Id },
            conversation);
    }

    [HttpGet("{conversationId}/messages")]
    public async Task<ActionResult<MessageHistoryResponse>> GetMessages(
        string conversationId,
        [FromQuery] DateTime? before,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Check if user is participant
        if (!await _conversationRepository.IsParticipantAsync(conversationId, userId, ct))
        {
            return Forbid();
        }

        limit = Math.Min(limit, 100);

        IEnumerable<MessageDocument> messages;
        if (before.HasValue)
        {
            messages = await _messageRepository.GetByConversationBeforeAsync(conversationId, before.Value, limit, ct);
        }
        else
        {
            messages = await _messageRepository.GetByConversationAsync(conversationId, limit, ct);
        }

        var response = new MessageHistoryResponse
        {
            ConversationId = conversationId,
            Messages = messages.Select(m => new MessageDto
            {
                Id = m.Id!,
                SenderId = m.SenderId,
                Type = m.Type,
                Text = m.Text,
                Voice = m.Voice != null ? new VoiceInfoDto
                {
                    BlobId = m.Voice.BlobId,
                    DurationMs = m.Voice.DurationMs,
                    MimeType = m.Voice.MimeType
                } : null,
                File = m.File != null ? new FileInfoDto
                {
                    BlobId = m.File.BlobId,
                    FileName = m.File.FileName,
                    MimeType = m.File.MimeType,
                    SizeBytes = m.File.SizeBytes
                } : null,
                ReplyToId = m.ReplyToId,
                CreatedAt = m.CreatedAt
            }),
            HasMore = messages.Count() >= limit
        };

        return Ok(response);
    }

    [HttpGet("{conversationId}/messages/{messageId}/replies")]
    public async Task<ActionResult<ThreadResponse>> GetMessageThread(
        string conversationId,
        string messageId,
        CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Check if user is participant
        if (!await _conversationRepository.IsParticipantAsync(conversationId, userId, ct))
        {
            return Forbid();
        }

        // Get original message
        var originalMessage = await _messageRepository.GetByIdAsync(messageId, ct);
        if (originalMessage == null || originalMessage.ConversationId != conversationId)
        {
            return NotFound(new { error = "Message not found" });
        }

        // Get all messages that reply to this message
        var replies = await _messageRepository.GetRepliesAsync(messageId, ct);

        var response = new ThreadResponse
        {
            OriginalMessage = new MessageDto
            {
                Id = originalMessage.Id!,
                SenderId = originalMessage.SenderId,
                Type = originalMessage.Type,
                Text = originalMessage.Text,
                Voice = originalMessage.Voice != null ? new VoiceInfoDto
                {
                    BlobId = originalMessage.Voice.BlobId,
                    DurationMs = originalMessage.Voice.DurationMs,
                    MimeType = originalMessage.Voice.MimeType
                } : null,
                File = originalMessage.File != null ? new FileInfoDto
                {
                    BlobId = originalMessage.File.BlobId,
                    FileName = originalMessage.File.FileName,
                    MimeType = originalMessage.File.MimeType,
                    SizeBytes = originalMessage.File.SizeBytes
                } : null,
                ReplyToId = originalMessage.ReplyToId,
                CreatedAt = originalMessage.CreatedAt
            },
            Replies = replies.Select(m => new MessageDto
            {
                Id = m.Id!,
                SenderId = m.SenderId,
                Type = m.Type,
                Text = m.Text,
                Voice = m.Voice != null ? new VoiceInfoDto
                {
                    BlobId = m.Voice.BlobId,
                    DurationMs = m.Voice.DurationMs,
                    MimeType = m.Voice.MimeType
                } : null,
                File = m.File != null ? new FileInfoDto
                {
                    BlobId = m.File.BlobId,
                    FileName = m.File.FileName,
                    MimeType = m.File.MimeType,
                    SizeBytes = m.File.SizeBytes
                } : null,
                ReplyToId = m.ReplyToId,
                CreatedAt = m.CreatedAt
            })
        };

        return Ok(response);
    }
}

public class CreateConversationRequest
{
    public string ServiceId { get; set; } = null!;
    public string? Title { get; set; }
    public List<string> ParticipantIds { get; set; } = new();
}

public class MessageHistoryResponse
{
    public string ConversationId { get; set; } = null!;
    public IEnumerable<MessageDto> Messages { get; set; } = null!;
    public bool HasMore { get; set; }
}

public class MessageDto
{
    public string Id { get; set; } = null!;
    public string SenderId { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? Text { get; set; }
    public VoiceInfoDto? Voice { get; set; }
    public FileInfoDto? File { get; set; }
    public string? ReplyToId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class VoiceInfoDto
{
    public string BlobId { get; set; } = null!;
    public int DurationMs { get; set; }
    public string MimeType { get; set; } = null!;
}

public class FileInfoDto
{
    public string BlobId { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string MimeType { get; set; } = null!;
    public long SizeBytes { get; set; }
}

public class ThreadResponse
{
    public MessageDto OriginalMessage { get; set; } = null!;
    public IEnumerable<MessageDto> Replies { get; set; } = null!;
}
