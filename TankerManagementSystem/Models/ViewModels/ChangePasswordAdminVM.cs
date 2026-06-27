using System.ComponentModel.DataAnnotations;

namespace TankerManagementSystem.Models.ViewModels
{
    public class ChangePasswordAdminVM
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }

        [Required(ErrorMessage = "New Password is required")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }
}
