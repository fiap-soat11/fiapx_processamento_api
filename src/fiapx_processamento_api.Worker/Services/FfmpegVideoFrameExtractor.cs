using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace fiapx_processamento_api.Worker.Services;

public class FfmpegVideoFrameExtractor : IVideoFrameExtractor
{
    private readonly ILogger<FfmpegVideoFrameExtractor> _logger;

    public FfmpegVideoFrameExtractor(ILogger<FfmpegVideoFrameExtractor> logger) => _logger = logger;

    public async Task<IReadOnlyList<string>> ExtractFramesAsync(string inputVideoPath, string outputDir, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDir);

        // 1 frame per second (ajuste se quiser)
        var outputPattern = Path.Combine(outputDir, "frame_%04d.jpg");
        var args = $"-hide_banner -loglevel error -i \"{inputVideoPath}\" -vf fps=1 \"{outputPattern}\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p is null) throw new InvalidOperationException("Não foi possível iniciar o ffmpeg.");

            var stdout = await p.StandardOutput.ReadToEndAsync();
            var stderr = await p.StandardError.ReadToEndAsync();

            await p.WaitForExitAsync(ct);

            if (p.ExitCode != 0)
            {
                _logger.LogWarning("ffmpeg retornou ExitCode={ExitCode}. stderr={Stderr}", p.ExitCode, stderr);
            }
        }
        catch (Exception ex)
        {
            // fallback: não quebra o pipeline; apenas gera zip vazio e registra warning
            _logger.LogWarning(ex, "ffmpeg não disponível ou falhou. Frames não serão extraídos.");
        }

        var frames = Directory.Exists(outputDir)
            ? Directory.GetFiles(outputDir, "frame_*.jpg").OrderBy(x => x).ToArray()
            : Array.Empty<string>();

        return frames;
    }
}
