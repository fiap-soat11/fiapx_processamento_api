namespace fiapx_processamento_api.Worker.Models;

public class VideoProcessingMessage
{
    // Must match fiapx_video_api Application.Interfaces.VideoProcessingMessage
    public int VideoId { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
