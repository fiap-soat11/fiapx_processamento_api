using Amazon.S3;
using Amazon.S3.Model;

namespace fiapx_processamento_api.Worker.Services;

public class S3StorageService : IS3StorageService
{
    private readonly IAmazonS3 _s3;

    public S3StorageService(IAmazonS3 s3) => _s3 = s3;

    public async Task DownloadAsync(string bucket, string key, string destinationPath, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        using var resp = await _s3.GetObjectAsync(bucket, key, ct);
        await using var fs = File.Create(destinationPath);
        await resp.ResponseStream.CopyToAsync(fs, ct);
    }

    public async Task UploadAsync(string bucket, string key, string filePath, string contentType, CancellationToken ct)
    {
        await using var fs = File.OpenRead(filePath);
        var req = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = fs,
            ContentType = contentType
        };
        await _s3.PutObjectAsync(req, ct);
    }
}
