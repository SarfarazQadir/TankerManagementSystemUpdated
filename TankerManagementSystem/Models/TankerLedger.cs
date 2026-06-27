using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models
{
    public class TankerLedger
    {
        [Key]
        public int Id { get; set; }

        public int TankerId { get; set; }
        public Tanker Tanker { get; set; }

        public DateTime TransactionDate { get; set; }
        public string ModuleName { get; set; } // e.g., "Trip Ledger"
        public int? ReferenceId { get; set; }   // TripLedgerId

        // Accounting Fields
        public decimal Credit { get; set; } // Company ke zime paise aye (Profit / Freight)
        public decimal Debit { get; set; }  // Company ne paise kaate (Loss / Expense / Advance)
        public decimal RunningBalance { get; set; } // Is entry ke baad ka net balance

        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
    }
}
