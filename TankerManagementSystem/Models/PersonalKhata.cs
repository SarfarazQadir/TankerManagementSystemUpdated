using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models
{
    public class PersonalKhata
    {
        [Key]
        public int Id { get; set; }
        public DateTime EntryDate { get; set; }
        public string PersonId { get; set; }
        public string Description { get; set; }
        public decimal AddAmount { get; set; }
        public decimal MinusAmount { get; set; }
        public decimal Balance { get; set; } // If zero show Nill
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
