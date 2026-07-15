namespace TankerManagementSystem.Models.ViewModels
{
    public class StatementRowVM
    {
        public DateTime Date { get; set; }
        public string RowType { get; set; } // "TRIP" ya "CASH"
        public string Details { get; set; }
        public decimal Freight { get; set; }   // TRIP ke liye Freight, CASH ke liye Credit
        public decimal Deduction { get; set; } // TRIP ke liye Deductions, CASH ke liye Debit
        public decimal Net { get; set; }
        public TripLedger? TripRef { get; set; } // sirf TRIP rows ke liye, CASH rows ke liye null
    }
}
