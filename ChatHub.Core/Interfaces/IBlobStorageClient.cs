namespace ChatHub.Core.Interfaces;

/// <summary>
/// S3-compatible blob storage client
/// </summary>
public interface IBlobStorageClient
{
    /// <summary>
    /// Upload a blob and return the blob ID
    /// </summary>
    Task<string> UploadAsync(Stream data, string contentType, CancellationToken ct = default);
    
    /// <summary>
    /// Download a blob by ID
    /// </summary>
    Task<Stream?> DownloadAsync(string blobId, CancellationToken ct = default);
    
    /// <summary>
    /// Get a presigned URL for downloading a blob
    /// </summary>
    Task<string> GetUrlAsync(string blobId, TimeSpan expiry, CancellationToken ct = default);
    
    /// <summary>
    /// Delete a blob by ID
    /// </summary>
    Task DeleteAsync(string blobId, CancellationToken ct = default);
}
