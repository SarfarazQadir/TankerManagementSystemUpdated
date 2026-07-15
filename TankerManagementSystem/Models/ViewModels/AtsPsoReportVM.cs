namespace TankerManagementSystem.Models.ViewModels
{
    public class AtsPsoReportVM
    {
        public int Id { get; set; }
        public DateTime EntryDate { get; set; }
        public string TankerNo { get; set; } = string.Empty;
        public string Capacity { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal TankerRunningBalance { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
    }
}