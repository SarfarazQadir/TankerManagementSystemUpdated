using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace TankerManagementSystem.Models
{
    public class TripExpense
    {
        [Key]
        public int Id { get; set; }
        public int? TripLedgerId { get; set; } // Nullable banaya kyunki pehle Trip Entry banti hai
        public TripLedger? TripLedger { get; set; }
        public int? TripEntryId { get; set; } // Naya field Trip Entry tracking ke liye
        public TripEntry? TripEntry { get; set; }
        public string ExpenseName { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
