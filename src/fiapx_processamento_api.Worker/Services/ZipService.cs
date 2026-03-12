using System.IO.Compression;

namespace fiapx_processamento_api.Worker.Services;

public class ZipService : IZipService
{
    public Task<string> CreateZipAsync(string sourceDir, string zipFilePath, CancellationToken ct)
    {
        // Valida que o diretório de origem existe
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Diretório de origem não encontrado: {sourceDir}");
        }

        // Garante que o diretório de destino existe
        var outputDir = Path.GetDirectoryName(zipFilePath);
        if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        if (File.Exists(zipFilePath)) File.Delete(zipFilePath);

        // ZipFile não suporta CancellationToken diretamente; mantemos simples
        ZipFile.CreateFromDirectory(sourceDir, zipFilePath, CompressionLevel.Fastest, includeBaseDirectory: false);
        return Task.FromResult(zipFilePath);
    }
}
