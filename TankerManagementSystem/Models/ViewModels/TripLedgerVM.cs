namespace TankerManagementSystem.Models.ViewModels
{
    public class TripLedgerVM
    {
        public int Id { get; set; }
        public DateTime TripDate { get; set; }
        public int TripEntryId { get; set; }
        public int ProductId { get; set; }
        public decimal Freight { get; set; }
        public decimal AdvanceCash { get; set; }
        public decimal ShortageLiters { get; set; }
        public decimal Shortage { get; set; }
        public decimal Commission { get; set; }
        public decimal Munshiana { get; set; }
        public decimal? AmountPay { get; set; }
        public string TokenNo { get; set; }
        public decimal GrandTotal { get; set; }
        public string Remarks { get; set; } = "N/A";
        public DateTime CreatedAt { get; set; }
        // Modified by AI
        // Date: 2026-07-21
        // Reason: L-04 — CreatedBy was declared as int? but ASP.NET Identity user IDs
        // are strings (GUIDs or usernames). This was a silent type mismatch. All other
        // models (CashLedger, TripLedger, PersonalKhata) use string? for CreatedBy.
        public string? CreatedBy { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal PayingAmount { get; set; }
    }
}
