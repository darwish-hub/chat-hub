using ChatHub.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatHub.Api.Controllers;

/// <summary>
/// File upload endpoints for voice and file attachments
/// </summary>
[ApiController]
[Route("api/upload")]
public class UploadController : ControllerBase
{
    private readonly IBlobStorageClient _blobStorage;
    private readonly ILogger<UploadController> _logger;
    
    public UploadController(
        IBlobStorageClient blobStorage,
        ILogger<UploadController> logger)
    {
        _blobStorage = blobStorage;
        _logger = logger;
    }
    
    /// <summary>
    /// Upload a voice file (pre-recorded)
    /// </summary>
    [HttpPost("voice")]
    [RequestSizeLimit(25 * 1024 * 1024)] // 25 MB
    public async Task<IActionResult> UploadVoice(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }
        
        // Validate content type
        var allowedTypes = new[] { "audio/opus", "audio/mpeg", "audio/wav", "audio/ogg", "audio/webm" };
        if (!allowedTypes.Contains(file.ContentType))
        {
            return BadRequest(new { error = "Invalid audio format" });
        }
        
        try
        {
            await using var stream = file.OpenReadStream();
            var blobId = await _blobStorage.UploadAsync(stream, file.ContentType);
            
            _logger.LogInformation("Voice file uploaded: {BlobId}", blobId);
            
            return Ok(new
            {
                blobId,
                fileName = file.FileName,
                mimeType = file.ContentType,
                sizeBytes = file.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload voice file");
            return StatusCode(500, new { error = "Upload failed" });
        }
    }
    
    /// <summary>
    /// Upload a file attachment
    /// </summary>
    [HttpPost("file")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }
        
        // Validate file name
        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            return BadRequest(new { error = "Invalid file name" });
        }
        
        // Sanitize file name
        var fileName = Path.GetFileName(file.FileName);
        
        try
        {
            await using var stream = file.OpenReadStream();
            var blobId = await _blobStorage.UploadAsync(stream, file.ContentType);
            
            // Generate presigned URL for download
            var url = await _blobStorage.GetUrlAsync(blobId, TimeSpan.FromHours(24));
            
            _logger.LogInformation("File uploaded: {BlobId} - {FileName}", blobId, fileName);
            
            return Ok(new
            {
                blobId,
                fileName,
                mimeType = file.ContentType,
                sizeBytes = file.Length,
                url
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file");
            return StatusCode(500, new { error = "Upload failed" });
        }
    }
}
