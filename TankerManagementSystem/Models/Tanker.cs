/*using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models
{
    public class Tanker
    {
        [Key]
        public int Id { get; set; }
        public string TankerNo { get; set; }
        public int OwnerId { get; set; }
        public TankerOwner Owner { get; set; }
        public string Model { get; set; }
        public string Capacity { get; set; }
        public decimal PreviousBalance { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
*/

using System.ComponentModel.DataAnnotations;
using TankerManagementSystem.Models;

namespace TankerManagementSystem.Models
{
    public class Tanker
    {
        [Key]
        public int Id { get; set; }
        public string TankerNo { get; set; }
        public int OwnerId { get; set; }
        public TankerOwner Owner { get; set; }
        public string Model { get; set; }
        public string Capacity { get; set; }

        // Sirf current running balance store karne ke liye (Taake har bar sum na karna pare)
        // (+) means Company owes Owner (Payable), (-) means Owner owes Company (Receivable)
        public decimal CurrentBalance { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}