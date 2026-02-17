namespace fiapx_processamento_api.Worker.Domain;

public class Video
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string S3Key { get; set; } = string.Empty;
    public string? S3ZipKey { get; set; }
    public VideoStatus Status { get; set; }
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
