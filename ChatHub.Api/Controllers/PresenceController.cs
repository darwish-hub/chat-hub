using ChatHub.Core.Interfaces;
using ChatHub.Infrastructure.Cache;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatHub.Api.Controllers;

[ApiController]
[Route("api/services")]
[Authorize]
public class PresenceController : ControllerBase
{
    private readonly IPresenceService _presenceService;
    private readonly ILogger<PresenceController> _logger;

    public PresenceController(
        IPresenceService presenceService,
        ILogger<PresenceController> logger)
    {
        _presenceService = presenceService;
        _logger = logger;
    }

    /// <summary>
    /// Get online users for a service
    /// </summary>
    [HttpGet("{serviceId}/online")]
    public async Task<ActionResult<OnlineUsersResponse>> GetOnlineUsers(
        string serviceId,
        CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var onlineUsers = await _presenceService.GetOnlineUsersAsync(serviceId, ct);
            
            var response = new OnlineUsersResponse
            {
                ServiceId = serviceId,
                OnlineUsers = onlineUsers.Select(u => new OnlineUserDto
                {
                    UserId = u.UserId,
                    DisplayName = u.UserId, // Could be enhanced to fetch from user service
                    LastSeen = u.Timestamp
                }).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get online users for service {ServiceId}", serviceId);
            return StatusCode(500, new { error = "Failed to retrieve online users" });
        }
    }

    /// <summary>
    /// Check if a specific user is online
    /// </summary>
    [HttpGet("{serviceId}/online/{targetUserId}")]
    public async Task<ActionResult<UserPresenceResponse>> GetUserPresence(
        string serviceId,
        string targetUserId,
        CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var isOnline = await _presenceService.IsUserOnlineAsync(serviceId, targetUserId, ct);
            
            return Ok(new UserPresenceResponse
            {
                ServiceId = serviceId,
                UserId = targetUserId,
                IsOnline = isOnline
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check presence for user {UserId} in service {ServiceId}", 
                targetUserId, serviceId);
            return StatusCode(500, new { error = "Failed to check user presence" });
        }
    }
}

public class OnlineUsersResponse
{
    public string ServiceId { get; set; } = null!;
    public List<OnlineUserDto> OnlineUsers { get; set; } = new();
}

public class OnlineUserDto
{
    public string UserId { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public DateTime LastSeen { get; set; }
}

public class UserPresenceResponse
{
    public string ServiceId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public bool IsOnline { get; set; }
}
