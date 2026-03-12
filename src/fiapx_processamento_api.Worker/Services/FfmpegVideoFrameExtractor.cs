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

        if (!File.Exists(inputVideoPath))
        {
            _logger.LogError("Arquivo de vídeo não encontrado: {InputPath}", inputVideoPath);
            return Array.Empty<string>();
        }

        var fileSize = new FileInfo(inputVideoPath).Length / 1024.0 / 1024.0;
        _logger.LogInformation("📹 Arquivo de entrada: {FileName} ({Size:F2}MB)", 
            Path.GetFileName(inputVideoPath), fileSize);

        // 1 frame per second (ajuste se quiser)
        var outputPattern = Path.Combine(outputDir, "frame_%04d.jpg");
        var args = $"-hide_banner -loglevel error -i \"{inputVideoPath}\" -vf fps=1 \"{outputPattern}\"";

        _logger.LogInformation("🎬 Iniciando ffmpeg...");

        Process? p = null;
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

            p = Process.Start(psi);
            if (p is null) throw new InvalidOperationException("Não foi possível iniciar o ffmpeg.");

            _logger.LogInformation("⚙️ Processo ffmpeg iniciado. PID={ProcessId}", p.Id);

            // Timeout de 5 minutos para extração de frames
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

            // Lê as saídas de forma assíncrona em paralelo para evitar deadlock
            var outputReadTask = Task.Run(async () =>
            {
                var stdout = await p.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                var stderr = await p.StandardError.ReadToEndAsync(timeoutCts.Token);
                return (stdout, stderr);
            }, timeoutCts.Token);

            // Task de heartbeat para monitorar progresso
            var heartbeatTask = Task.Run(async () =>
            {
                while (!p.HasExited && !timeoutCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), timeoutCts.Token).ConfigureAwait(false);
                    if (!p.HasExited)
                    {
                        var currentFrames = Directory.Exists(outputDir) ? Directory.GetFiles(outputDir, "frame_*.jpg").Length : 0;
                        _logger.LogInformation("⏳ ffmpeg ainda processando... Frames gerados até agora: {FrameCount}", currentFrames);
                    }
                }
            }, timeoutCts.Token);

            try
            {
                await p.WaitForExitAsync(timeoutCts.Token);
                
                _logger.LogInformation("✅ ffmpeg finalizou. Aguardando leitura de saídas...");
                var (stdout, stderr) = await outputReadTask;

                if (p.ExitCode != 0)
                {
                    _logger.LogWarning("ffmpeg retornou ExitCode={ExitCode}. stderr={Stderr}", p.ExitCode, stderr);
                }
                else
                {
                    _logger.LogInformation("✅ ffmpeg concluído com sucesso.");
                }
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                _logger.LogWarning("⏱️ ffmpeg ultrapassou o timeout de 5 minutos. Encerrando processo.");
                KillProcessSafely(p);
                throw new TimeoutException("Extração de frames ultrapassou o tempo limite de 5 minutos.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogWarning("🛑 Extração de frames cancelada.");
                KillProcessSafely(p);
                throw;
            }
        }
        catch (TimeoutException)
        {
            // Re-lança timeout para tratamento específico no caller
            throw;
        }
        catch (OperationCanceledException)
        {
            // Re-lança cancelamento
            throw;
        }
        catch (Exception ex)
        {
            // fallback: não quebra o pipeline; apenas gera zip vazio e registra warning
            _logger.LogWarning(ex, "ffmpeg não disponível ou falhou. Frames não serão extraídos.");
            
            if (p != null && !p.HasExited)
            {
                KillProcessSafely(p);
            }
        }
        finally
        {
            p?.Dispose();
        }

        var frames = Directory.Exists(outputDir)
            ? Directory.GetFiles(outputDir, "frame_*.jpg").OrderBy(x => x).ToArray()
            : Array.Empty<string>();

        _logger.LogInformation("Total de frames extraídos: {FrameCount}", frames.Length);

        return frames;
    }

    private void KillProcessSafely(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _logger.LogInformation("Processo ffmpeg finalizado.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao finalizar processo ffmpeg.");
        }
    }
}
