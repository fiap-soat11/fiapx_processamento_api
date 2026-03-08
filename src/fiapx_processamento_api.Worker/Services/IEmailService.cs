namespace fiapx_processamento_api.Worker.Services;

public interface IEmailService
{
    Task SendSuccessAsync(string to, int videoId, string s3ZipKey, CancellationToken ct);
    Task SendFailureAsync(string to, int videoId, string errorMessage, CancellationToken ct);
}
