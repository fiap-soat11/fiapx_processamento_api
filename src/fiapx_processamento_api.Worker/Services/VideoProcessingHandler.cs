using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using fiapx_processamento_api.Worker.Configuration;
using fiapx_processamento_api.Worker.Domain;
using fiapx_processamento_api.Worker.Infrastructure.Repositories;
using fiapx_processamento_api.Worker.Models;

namespace fiapx_processamento_api.Worker.Services;

public class VideoProcessingHandler : IVideoProcessingHandler
{
    private readonly ILogger<VideoProcessingHandler> _logger;
    private readonly IVideoRepository _repo;
    private readonly AwsS3Settings _s3Settings;
    private readonly IS3StorageService _s3;
    private readonly IVideoFrameExtractor _extractor;
    private readonly IZipService _zip;
    private readonly IEmailService _email;

    public VideoProcessingHandler(
        ILogger<VideoProcessingHandler> logger,
        IVideoRepository repo,
        IOptions<AwsS3Settings> s3Settings,
        IS3StorageService s3,
        IVideoFrameExtractor extractor,
        IZipService zip,
        IEmailService email)
    {
        _logger = logger;
        _repo = repo;
        _s3Settings = s3Settings.Value;
        _s3 = s3;
        _extractor = extractor;
        _zip = zip;
        _email = email;
    }

    public async Task<bool> HandleAsync(string messageBody, CancellationToken ct)
    {
        VideoProcessingMessage msg;
        try
        {
            msg = JsonSerializer.Deserialize<VideoProcessingMessage>(messageBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Mensagem SQS inválida (null).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao desserializar mensagem SQS. Body={Body}", messageBody);
            // Mensagem inválida: deletar para não ficar em loop
            return true;
        }

        var video = await _repo.GetByIdAsync(msg.VideoId, ct);
        if (video is null)
        {
            _logger.LogWarning("Vídeo {VideoId} não encontrado no banco. Deletando mensagem.", msg.VideoId);
            return true;
        }

        if (video.Status == VideoStatus.Processed)
        {
            _logger.LogInformation("Vídeo {VideoId} já Processed (idempotência). Deletando mensagem.", msg.VideoId);
            return true;
        }

        var bucket = string.IsNullOrWhiteSpace(msg.BucketName) ? _s3Settings.BucketName : msg.BucketName;

        var workDir = Path.Combine(Path.GetTempPath(), "fiapx_processamento", msg.VideoId.ToString("N"));
        var inputPath = Path.Combine(workDir, "input", video.FileName);
        var framesDir = Path.Combine(workDir, "frames");
        var zipPath = Path.Combine(workDir, "output", $"{msg.VideoId:N}.zip");

        try
        {
            video.Status = VideoStatus.Processing;
            video.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(video, ct);

            _logger.LogInformation("Baixando do S3. bucket={Bucket} key={Key}", bucket, msg.S3Key);
            await _s3.DownloadAsync(bucket, msg.S3Key, inputPath, ct);

            _logger.LogInformation("Extraindo frames para {FramesDir}", framesDir);
            await _extractor.ExtractFramesAsync(inputPath, framesDir, ct);

            _logger.LogInformation("Criando ZIP {ZipPath}", zipPath);
            await _zip.CreateZipAsync(framesDir, zipPath, ct);

            var s3ZipKey = $"videos/{msg.VideoId}/processed/{msg.VideoId:N}.zip";
            _logger.LogInformation("Enviando ZIP para S3. key={ZipKey}", s3ZipKey);
            await _s3.UploadAsync(bucket, s3ZipKey, zipPath, "application/zip", ct);

            video.Status = VideoStatus.Processed;
            video.S3ZipKey = s3ZipKey;
            video.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(video, ct);

            // Notificação: por padrão desabilitada; você pode integrar com usuario_api para resolver o e-mail do usuário
            await _email.SendSuccessAsync(to: "user@example.com", videoId: msg.VideoId, s3ZipKey: s3ZipKey, ct);

            _logger.LogInformation("Processamento concluído para {VideoId}", msg.VideoId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro processando vídeo {VideoId}", msg.VideoId);

            try
            {
                video.Status = VideoStatus.Failed;
                video.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(video, ct);
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "Falha ao atualizar status Failed para {VideoId}", msg.VideoId);
            }

            try
            {
                await _email.SendFailureAsync(to: "user@example.com", videoId: msg.VideoId, errorMessage: ex.Message, ct);
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "Falha ao enviar e-mail de erro para {VideoId}", msg.VideoId);
            }

            // Não deletar para retry / DLQ
            return false;
        }
        finally
        {
            try { if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true); } catch { }
        }
    }
}
