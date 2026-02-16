namespace fiapx_processamento_api.Worker.Configuration;

public class AwsSqsSettings
{
    public string QueueUrl { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}
