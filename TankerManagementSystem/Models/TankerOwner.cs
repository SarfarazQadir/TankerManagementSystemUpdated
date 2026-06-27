using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models
{
    public class TankerOwner
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string CNIC { get; set; }
        public string Address { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
