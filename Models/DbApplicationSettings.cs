namespace CadProcessorService.Models;

public class DbApplicationSettings
{
    public int ApplicationId { get; set; }
    public bool HasSettings { get; set; }
    public string ServiceMode { get; set; } = "Polling";
    public int PollIntervalSeconds { get; set; } = 20;
    public int SystemUserId { get; set; } = 0;
    public bool EnableParallelPipeline { get; set; }
    public string LogFolder { get; set; } = string.Empty;
    public int MaxQueueSize { get; set; }
    public int WorkerCount { get; set; }
    public Dictionary<string, string> AdditionalSettings { get; set; } = new();
}
