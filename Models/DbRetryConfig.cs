namespace CadProcessorService.Models;

public class DbRetryConfig
{
    public int RetrySettingId { get; set; }
    public int ApplicationId { get; set; }
    public int ProviderId { get; set; }
    public bool Enabled { get; set; }
    public int MaxAttempts { get; set; }
    public int DelaySeconds { get; set; }
}
