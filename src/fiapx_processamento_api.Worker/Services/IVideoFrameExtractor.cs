namespace fiapx_processamento_api.Worker.Services;

public interface IVideoFrameExtractor
{
    /// <summary>Extract frames from a local video file to outputDir. Returns list of frame file paths (may be empty).</summary>
    Task<IReadOnlyList<string>> ExtractFramesAsync(string inputVideoPath, string outputDir, CancellationToken ct);
}
