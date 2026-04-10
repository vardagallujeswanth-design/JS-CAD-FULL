namespace CadProcessorService.Models;

public class DbProviderConfig
{
    public int ProviderId { get; set; }
    public int ApplicationId { get; set; }
    public string ProviderCode { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string IdentificationPath { get; set; } = "";
    public string IdentifierValue { get; set; } = "";
    public string CallerNameNode { get; set; } = "";
    public string CallNumberNode { get; set; } = "";
    public string PrimaryOfficerNameNode { get; set; } = "";
    public string OfficersNode { get; set; } = "";
    public string? CallerPhoneNode { get; set; }
}
