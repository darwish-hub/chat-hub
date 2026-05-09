using Amazon.S3;
using Amazon.S3.Model;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Options;

namespace ChatHub.Infrastructure.Storage;

/// <summary>
/// S3-compatible blob storage client
/// </summary>
public class S3BlobStorageClient : IBlobStorageClient
{
    private readonly IAmazonS3 _s3Client;
    private readonly StorageSettings _settings;
    private readonly ILogger<S3BlobStorageClient> _logger;
    
    public S3BlobStorageClient(
        IOptions<StorageSettings> settings,
        ILogger<S3BlobStorageClient> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        var config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = true,
            UseHttp = !_settings.UseSsl
        };
        
        _s3Client = new AmazonS3Client(
            _settings.AccessKey,
            _settings.SecretKey,
            config);
    }
    
    public async Task<string> UploadAsync(Stream data, string contentType, CancellationToken ct = default)
    {
        var blobId = Guid.NewGuid().ToString();
        
        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = blobId,
            InputStream = data,
            ContentType = contentType
        };
        
        await _s3Client.PutObjectAsync(request, ct);
        
        _logger.LogDebug("Uploaded blob {BlobId} with content type {ContentType}", blobId, contentType);
        
        return blobId;
    }
    
    public async Task<Stream?> DownloadAsync(string blobId, CancellationToken ct = default)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(_settings.BucketName, blobId, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
    public async Task<string> GetUrlAsync(string blobId, TimeSpan expiry, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = blobId,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET
        };
        
        return await _s3Client.GetPreSignedURLAsync(request);
    }
    
    public async Task DeleteAsync(string blobId, CancellationToken ct = default)
    {
        await _s3Client.DeleteObjectAsync(_settings.BucketName, blobId, ct);
        
        _logger.LogDebug("Deleted blob {BlobId}", blobId);
    }
}
