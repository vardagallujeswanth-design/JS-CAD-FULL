namespace CadProcessorService.Models;

public class ServiceMetaDataDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceMode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> AdditionalSettings { get; set; } = new();
}
