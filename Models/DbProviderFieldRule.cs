namespace CadProcessorService.Models
{
	public class DbProviderFieldRule
	{
		public int RuleId { get; set; }
		public int ProviderId { get; set; }
		public int ProcedureId { get; set; }
		public string ParameterName { get; set; } = string.Empty;
		public string RuleType { get; set; } = string.Empty;
		public string? RuleValue { get; set; }
        public string? RuleCategory { get; set; }
        public int RuleOrder { get; set; } = 1;
		public bool IsActive { get; set; } = false;
	}
}
