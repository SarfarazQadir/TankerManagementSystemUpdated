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

        // Modified by AI
        // Date: 2026-07-21
        // Reason: M-07 — DateTime.Today uses the server's local timezone (which may be
        // UTC or another timezone), NOT Pakistan Standard Time. Removed the default so the
        // controller sets EntryDate explicitly using DateTimeHelper.GetPakistanToday().
        // This prevents incorrect date defaults when the server runs outside Pakistan timezone.
        [Required]
        [DataType(DataType.Date)]
        public DateTime EntryDate { get; set; }
        public int? TankerLedgerId { get; set; }
        // FIX Issue 13: Removed DateTime.Now default to standardize timezone handling - Done by AntiGravity on 2026-07-18 08:15 PST
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}