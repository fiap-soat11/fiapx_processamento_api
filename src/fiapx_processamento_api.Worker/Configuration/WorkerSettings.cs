namespace fiapx_processamento_api.Worker.Configuration;

public class WorkerSettings
{
    public int MaxConcurrency { get; set; } = 4;
    public int MaxMessagesPerPoll { get; set; } = 10;
    public int WaitTimeSeconds { get; set; } = 20;
    public int VisibilityTimeoutSeconds { get; set; } = 180;
}
