namespace ChatHub.Core.Interfaces;

public interface IBlobStorageClient
{
    Task<string> UploadAsync(string blobId, Stream data, string contentType, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string blobId, CancellationToken ct = default);
    Task<bool> DeleteAsync(string blobId, CancellationToken ct = default);
    Task<string> GetPreSignedUrlAsync(string blobId, TimeSpan expiry, CancellationToken ct = default);
}
