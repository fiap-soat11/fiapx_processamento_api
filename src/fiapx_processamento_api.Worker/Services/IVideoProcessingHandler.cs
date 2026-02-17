namespace fiapx_processamento_api.Worker.Services;

public interface IVideoProcessingHandler
{
    /// <summary>Handle a raw SQS message body. Returns true if message should be deleted.</summary>
    Task<bool> HandleAsync(string messageBody, CancellationToken ct);
}
