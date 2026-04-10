namespace CadProcessorService.Models;

public class DbFieldMapping
{
    public int MappingId { get; set; }
    public int ApplicationId { get; set; }
    public int ProviderId { get; set; }
    public int ProcedureId { get; set; }
    public string ParameterName { get; set; } = "";
    public string XmlPath { get; set; } = "";
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsActive { get; set; } = true;
}
