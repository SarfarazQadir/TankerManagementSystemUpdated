using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        // NEW: Explicit flag — replaces fragile "Description contains 'Office'" text matching.
        // True = this Debit is a genuine company-level expense (rent, salaries, utilities, etc.)
        // that should count toward Dashboard "Total Expenses". Should normally be false/unused
        // when TankerId or KhataPersonId is set, since those already have their own accounting.
        public bool IsGeneralExpense { get; set; }

        // TankerId can be null if it's a general expense
        public int? TankerId { get; set; }

        [ForeignKey("TankerId")]
        [ValidateNever] // This prevents HTTP 400 Validation Errors
        public virtual Tanker? Tanker { get; set; }
        public int? KhataPersonId { get; set; }

        [ForeignKey("KhataPersonId")]
        [ValidateNever]
        public virtual KhataPerson? KhataPerson { get; set; }
    }
}
