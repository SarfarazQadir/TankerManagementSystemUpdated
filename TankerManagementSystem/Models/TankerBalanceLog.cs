using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models
{
    public class TankerBalanceLog
    {
        [Key]
        public int Id { get; set; }
        public int TankerId { get; set; }
        public Tanker Tanker { get; set; }
        public string TransactionType { get; set; }
        public string ModuleName { get; set; }
        public int? ReferenceId { get; set; }
        public decimal PreviousBalance { get; set; }
        public decimal Amount { get; set; }
        public decimal NewBalance { get; set; }
        public string Description { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
