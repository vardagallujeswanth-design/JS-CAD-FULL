namespace CadProcessorService.Models;

public class CadProcessingOptions
{
   public string ServiceMode { get; set; } = "Polling";
   public int PollIntervalSeconds { get; set; } = 30;
   public int SystemUserId { get; set; } = 0;
   public string ApplicationId { get; set; } = "";
  
}
