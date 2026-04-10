namespace CadProcessorService.Infrastructure.Logging;

public class LoggingOptions
{
    public string FallbackLogFolder { get; set; } = "C:\\CAD_Logs";
    public int DaysToKeepLogs { get; set; } = 10;
}
