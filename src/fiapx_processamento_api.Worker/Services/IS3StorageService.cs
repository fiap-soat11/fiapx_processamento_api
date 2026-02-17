namespace fiapx_processamento_api.Worker.Services;

public interface IS3StorageService
{
    Task DownloadAsync(string bucket, string key, string destinationPath, CancellationToken ct);
    Task UploadAsync(string bucket, string key, string filePath, string contentType, CancellationToken ct);
}
