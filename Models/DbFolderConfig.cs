namespace CadProcessorService.Models;

public class DbFolderConfig
{
    public int FolderConfigId { get; set; }
    public int ApplicationId { get; set; }
    public int ProviderId { get; set; }
    public string SourceFolder { get; set; } = "";
    public string DoneFolder { get; set; } = "";
    public string ErrorFolder { get; set; } = "";
    public string RetryFolder { get; set; } = "";
    public string OtherAgencyFolder { get; set; } = "";
}
