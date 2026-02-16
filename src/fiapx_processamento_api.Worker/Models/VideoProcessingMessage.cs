namespace fiapx_processamento_api.Worker.Models;

public class VideoProcessingMessage
{
    public Guid VideoId { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
