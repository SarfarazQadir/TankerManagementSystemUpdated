using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models
{
    public class CashLedger
    {
        [Key]
        public int Id { get; set; }
        public DateTime EntryDate { get; set; }
        public int PersonId { get; set; } // User
        public string Description { get; set; }
        public decimal Credit { get; set; }
        public decimal Debit { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
