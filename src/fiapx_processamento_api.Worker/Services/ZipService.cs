using System.IO.Compression;

namespace fiapx_processamento_api.Worker.Services;

public class ZipService : IZipService
{
    public Task<string> CreateZipAsync(string sourceDir, string zipFilePath, CancellationToken ct)
    {
        if (File.Exists(zipFilePath)) File.Delete(zipFilePath);

        // ZipFile não suporta CancellationToken diretamente; mantemos simples
        ZipFile.CreateFromDirectory(sourceDir, zipFilePath, CompressionLevel.Fastest, includeBaseDirectory: false);
        return Task.FromResult(zipFilePath);
    }
}
