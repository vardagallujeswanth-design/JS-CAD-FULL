namespace CadProcessorService.Models;

public class DbServiceMetaData
{
    public int CdServiceMetaDataId { get; set; }
    public int ApplicationId { get; set; }
    public int ProviderId { get; set; }
    public int OperatorType { get; set; }
    public string Value { get; set; } = string.Empty;
    public string ORINum { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public int UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
