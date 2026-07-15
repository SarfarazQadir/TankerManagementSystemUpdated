using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models
{
    public class KhataPerson
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }
        public string? ContactNumber { get; set; }
        public string? Address { get; set; }
        public string? Description { get; set; }

        // Running balance — jaise Tanker.CurrentBalance
        public decimal CurrentBalance { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}