using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models
{
    public class CommissionSetup
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Percentage { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
