namespace fiapx_processamento_api.Worker.Domain.Entities;

/// <summary>
/// Must match fiapx_video_api Domain.Entities.VideoProcessing.
/// Table: video_processings
/// </summary>
public class VideoProcessing
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string Status { get; set; } = VideoProcessingStatuses.Pending;
    public string S3InputPath { get; set; } = string.Empty;
    public string? S3OutputPath { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public User? User { get; set; }
}

public static class VideoProcessingStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
