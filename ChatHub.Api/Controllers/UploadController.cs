using ChatHub.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ChatHub.Api.Controllers;

[ApiController]
[Route("api/upload")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IBlobStorageClient _blobStorage;
    private readonly ILogger<UploadController> _logger;

    // Maximum file size
    private const long MaxFileSize = 100 * 1024 * 1024; // 100 MB

    // Allowed MIME types
    private static readonly HashSet<string> AllowedFileTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Documents
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain",
        "text/csv",
        // Images
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/svg+xml",
        // Videos
        "video/mp4",
        "video/webm",
        "video/ogg",
        "video/quicktime",
        // Audio (voice)
        "audio/opus",
        "audio/ogg",
        "audio/mpeg",
        "audio/mp3",
        "audio/wav",
        "audio/webm",
        "audio/aac",
        "audio/flac",
        // Archives
        "application/zip",
        "application/x-zip-compressed",
        "application/gzip",
        "application/x-tar"
    };

    public UploadController(
        IBlobStorageClient blobStorage,
        ILogger<UploadController> logger)
    {
        _blobStorage = blobStorage;
        _logger = logger;
    }

    /// <summary>
    /// Upload an attachment (file, voice, video, or image)
    /// </summary>
    [HttpPost("file")]
    [RequestSizeLimit(MaxFileSize)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadResponse>> UploadFile(
        IFormFile file,
        [FromForm] int? durationMs,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }

        // Validate file size
        if (file.Length > MaxFileSize)
        {
            return BadRequest(new { error = $"File exceeds maximum size of {MaxFileSize / 1024 / 1024} MB" });
        }

        // Validate MIME type
        if (!AllowedFileTypes.Contains(file.ContentType))
        {
            _logger.LogWarning("Uploading file with unverified MIME type: {ContentType}", file.ContentType);
            // Don't reject, just log - clients may have custom file types
        }

        // Generate blob ID
        var blobId = Guid.NewGuid().ToString();

        try
        {
            // Stream file directly to S3
            using var stream = file.OpenReadStream();
            await _blobStorage.UploadAsync(blobId, stream, file.ContentType, ct);

            _logger.LogInformation("Attachment uploaded: {BlobId}, name: {FileName}, size: {Size}, type: {Type}, durationMs: {DurationMs}",
                blobId, file.FileName, file.Length, file.ContentType, durationMs);

            return Ok(new UploadResponse
            {
                BlobId = blobId,
                FileName = file.FileName,
                MimeType = file.ContentType,
                SizeBytes = file.Length,
                DurationMs = durationMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload attachment");
            return StatusCode(500, new { error = "Failed to upload attachment" });
        }
    }

    /// <summary>
    /// Download a file by blob ID
    /// </summary>
    [HttpGet("download/{blobId}")]
    [AllowAnonymous] // Or require auth based on your needs
    public async Task<IActionResult> Download(string blobId, CancellationToken ct)
    {
        try
        {
            // Try to get pre-signed URL first (for S3)
            var preSignedUrl = await _blobStorage.GetPreSignedUrlAsync(blobId, TimeSpan.FromMinutes(5), ct);

            if (!string.IsNullOrEmpty(preSignedUrl))
            {
                return Redirect(preSignedUrl);
            }

            // Otherwise stream directly
            var stream = await _blobStorage.DownloadAsync(blobId, ct);

            if (stream == null)
            {
                return NotFound(new { error = "File not found" });
            }

            return File(stream, MediaTypeNames.Application.Octet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file: {BlobId}", blobId);
            return StatusCode(500, new { error = "Failed to download file" });
        }
    }
}

public class UploadResponse
{
    public string BlobId { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string MimeType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public int? DurationMs { get; set; }
}
