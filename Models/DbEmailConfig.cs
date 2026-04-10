namespace CadProcessorService.Models;

public class DbEmailConfig
{
    public int EmailSettingId { get; set; }
    public int ApplicationId { get; set; }
    public int ProviderId { get; set; }
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public bool EnableSsl { get; set; }
    public string FromEmail { get; set; } = "";
    public string ToEmail { get; set; } = "";
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool SendOnSuccess { get; set; }
    public bool SendOnFailure { get; set; }
}
