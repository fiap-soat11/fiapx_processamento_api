using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using fiapx_processamento_api.Worker.Configuration;
using fiapx_processamento_api.Worker.Domain.Entities;
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

        if (string.Equals(video.Status, VideoProcessingStatuses.Completed, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Vídeo {VideoId} já Completed (idempotência). Deletando mensagem.", msg.VideoId);
            return true;
        }

        var bucket = string.IsNullOrWhiteSpace(msg.BucketName) ? _s3Settings.BucketName : msg.BucketName;



        var workDir = Path.Combine(Path.GetTempPath(), "fiapx_processamento", msg.VideoId.ToString());
        var inputFileName = Path.GetFileName(msg.S3Key);
        if (string.IsNullOrWhiteSpace(inputFileName)) inputFileName = "input.mp4";
        var inputPath = Path.Combine(workDir, "input", inputFileName);
        var framesDir = Path.Combine(workDir, "frames");
        var outputDir = Path.Combine(workDir, "output");
        var zipPath = Path.Combine(outputDir, $"{msg.VideoId}.zip");

        try
        {
            video.Status = VideoProcessingStatuses.Processing;
            await _repo.UpdateAsync(video, ct);

            _logger.LogInformation("Baixando do S3. bucket={Bucket} key={Key}", bucket, msg.S3Key);
            await _s3.DownloadAsync(bucket, msg.S3Key, inputPath, ct);

            _logger.LogInformation("Extraindo frames para {FramesDir}", framesDir);
            await _extractor.ExtractFramesAsync(inputPath, framesDir, ct);

            var frameFiles = Directory.GetFiles(framesDir, "*.jpg");
            _logger.LogInformation("{FrameCount} frames extraídos.", frameFiles.Length);

            _logger.LogInformation("Criando ZIP {ZipPath}", zipPath);
            await _zip.CreateZipAsync(framesDir, zipPath, ct);

            // Align output path with fiapx_video_api upload format: videos/{prefix}/original/{file}
            var prefix = TryExtractPrefixFromS3Key(msg.S3Key) ?? msg.VideoId.ToString();
            var s3ZipKey = $"videos/{prefix}/processed/{prefix}.zip";
            _logger.LogInformation("Enviando ZIP para S3. key={ZipKey}", s3ZipKey);
            await _s3.UploadAsync(bucket, s3ZipKey, zipPath, "application/zip", ct);

            video.Status = VideoProcessingStatuses.Completed;
            video.S3OutputPath = s3ZipKey;
            video.CompletedAt = DateTime.UtcNow;
            video.FailureReason = null;
            await _repo.UpdateAsync(video, ct);

            // Notificação: tenta resolver pelo e-mail cadastrado na tabela users
            var emailTo = await _repo.GetUserEmailAsync(video.UserId, ct);
            if (!string.IsNullOrWhiteSpace(emailTo))
                await _email.SendSuccessAsync(to: emailTo, videoId: msg.VideoId, s3ZipKey: s3ZipKey, ct);

            _logger.LogInformation("Processamento concluído para {VideoId}", msg.VideoId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro processando vídeo {VideoId}", msg.VideoId);

            try
            {
                video.Status = VideoProcessingStatuses.Failed;
                video.FailureReason = ex.Message;
                await _repo.UpdateAsync(video, ct);
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "Falha ao atualizar status Failed para {VideoId}", msg.VideoId);
            }

            try
            {
                var emailTo = await _repo.GetUserEmailAsync(video.UserId, ct);
                if (!string.IsNullOrWhiteSpace(emailTo))
                    await _email.SendFailureAsync(to: emailTo, videoId: msg.VideoId, errorMessage: ex.Message, ct);
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

    private static string? TryExtractPrefixFromS3Key(string s3Key)
    {
        // expected: videos/{prefix}/original/{filename}
        var parts = s3Key.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2 && string.Equals(parts[0], "videos", StringComparison.OrdinalIgnoreCase))
            return parts[1];
        return null;
    }
}
