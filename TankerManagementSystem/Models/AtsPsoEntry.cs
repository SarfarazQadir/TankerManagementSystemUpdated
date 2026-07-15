using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TankerManagementSystem.Models
{
    public class AtsPsoEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TankerId { get; set; }

        [ForeignKey("TankerId")]
        public Tanker? Tanker { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime EntryDate { get; set; } = DateTime.Today; // User selected date
        public int? TankerLedgerId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}