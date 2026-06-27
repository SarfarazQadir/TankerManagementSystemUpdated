namespace TankerManagementSystem.Models.ViewModels
{
    public class CashLedgerVM
    {
        public int Id { get; set; }

        public DateTime EntryDate { get; set; }

        public string Description { get; set; }

        public decimal Credit { get; set; }

        public decimal Debit { get; set; }

        public decimal Balance { get; set; }

        public string CreatedByName { get; set; }
    }
}
