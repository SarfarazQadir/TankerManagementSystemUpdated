using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }
        public string ProductName { get; set; }   // PMG, Diesel, Oil etc
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
