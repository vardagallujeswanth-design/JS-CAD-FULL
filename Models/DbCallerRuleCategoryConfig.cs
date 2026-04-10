namespace CadProcessorService.Models
{
    public class DbCallerRuleCategoryConfig
    {
        public int ConfigId { get; set; }
        public int ApplicationId { get; set; }
        public string CategoryName { get; set; } = "";   // "CallerNode"
        public string CategoryRole { get; set; } = "";   // "NodeLocator"
        public bool IsRequired { get; set; }
        public string? FallbackRole { get; set; }
        public bool IsActive { get; set; }
    }
}