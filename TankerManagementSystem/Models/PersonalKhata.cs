using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TankerManagementSystem.Models
{
    public class PersonalKhata
    {
        [Key]
        public int Id { get; set; }
        public DateTime EntryDate { get; set; }
        public int KhataPersonId { get; set; }

        [ForeignKey("KhataPersonId")]
        [ValidateNever]
        public virtual KhataPerson? KhataPerson { get; set; }
        public string Description { get; set; }
        public decimal AddAmount { get; set; }
        public decimal MinusAmount { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        // Ye track karega ke ye entry CashLedger se auto-generated hai (Tanker pattern jaisa)
        public string? ModuleName { get; set; }
        public int? ReferenceId { get; set; }
    }
}