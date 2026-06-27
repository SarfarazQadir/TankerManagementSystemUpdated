using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models
{
    public class TripLedger
    {
        [Key]
        public int Id { get; set; }
        public DateTime TripDate { get; set; }
        public int TripEntryId { get; set; }
        public TripEntry TripEntryFk { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public decimal Freight { get; set; }
        public decimal AdvanceCash { get; set; }
        public decimal Shortage { get; set; }
        public decimal Commission { get; set; }
        public decimal Munshiana { get; set; }
        //public decimal? PreviousBalance { get; set; } = null;
        public decimal? AmountPay { get; set; } = null;
        public string TokenNo { get; set; }
        public decimal GrandTotal { get; set; }
        public string Remarks { get; set; } = null;
        public List<TripExpense> Expenses { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public decimal TotalDeductions { get; set; }
    }
}
