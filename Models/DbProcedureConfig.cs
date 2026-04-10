namespace CadProcessorService.Models;

public class DbProcedureConfig
{
    public int ProcedureId { get; set; }
    public string ProcedureName { get; set; } = "";
    public int ExecutionOrder { get; set; }
    public bool IsRepeatable { get; set; }
    public List<DbProviderFieldRule> FieldRules { get; set; } = new();
}
