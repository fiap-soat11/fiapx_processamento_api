using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using fiapx_processamento_api.Worker.Configuration;

namespace fiapx_processamento_api.Worker.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task SendSuccessAsync(string to, Guid videoId, string s3ZipKey, CancellationToken ct) =>
        SendAsync(
            to,
            $"Processamento concluído - Vídeo {videoId}",
            $"Seu vídeo foi processado com sucesso.\nZIP no S3: {s3ZipKey}",
            ct);

    public Task SendFailureAsync(string to, Guid videoId, string errorMessage, CancellationToken ct) =>
        SendAsync(
            to,
            $"Falha no processamento - Vídeo {videoId}",
            $"Ocorreu um erro no processamento.\nErro: {errorMessage}",
            ct);

    private async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("E-mail desabilitado. (Assunto: {Subject}) Para: {To}", subject, to);
            return;
        }

        var from = string.IsNullOrWhiteSpace(_settings.From) ? _settings.User : _settings.From;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        var secure = _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(_settings.Host, _settings.Port, secure, ct);
        await client.AuthenticateAsync(_settings.User, _settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
