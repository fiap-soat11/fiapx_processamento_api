namespace fiapx_processamento_api.Worker.Configuration;

public class EmailSettings
{
    public bool Enabled { get; set; } = false;

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = false;

    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;
}
