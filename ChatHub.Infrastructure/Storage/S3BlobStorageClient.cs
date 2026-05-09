using Amazon.S3;
using Amazon.S3.Model;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;

namespace ChatHub.Infrastructure.Storage;

public class S3BlobStorageClient : IBlobStorageClient
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucket;

    public S3BlobStorageClient(StorageSettings settings)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = settings.Endpoint,
            ForcePathStyle = settings.ForcePathStyle,
            AuthenticationRegion = settings.Region
        };

        _s3Client = new AmazonS3Client(settings.AccessKey, settings.SecretKey, config);
        _bucket = settings.Bucket;
    }

    public async Task<string> UploadAsync(string blobId, Stream data, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = blobId,
            InputStream = data,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(request, ct);
        return blobId;
    }

    public async Task<Stream?> DownloadAsync(string blobId, CancellationToken ct = default)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(_bucket, blobId, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string blobId, CancellationToken ct = default)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(_bucket, blobId, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetPreSignedUrlAsync(string blobId, TimeSpan expiry, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = blobId,
            Expires = DateTime.UtcNow.Add(expiry)
        };

        return _s3Client.GetPreSignedURL(request);
    }
}
