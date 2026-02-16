namespace fiapx_processamento_api.Worker.Services;

public interface IZipService
{
    Task<string> CreateZipAsync(string sourceDir, string zipFilePath, CancellationToken ct);
}
