namespace TankerManagementSystem.Models.ViewModels
{
    // User Create karne ke liye ViewModel
    public class UserCreateMVCVM
    {
        public string FullName { get; set; } // Agar default IdentityUser me FullName nahi hai, to ye option optional rakh sakte hain ya skip kar sakte hain
        public string Email { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool EnableOTP { get; set; } // TwoFactorEnabled ke liye checkbox
        public string SelectedRole { get; set; } = "User"; // Default Role
    }

    // User Edit aur Module Access manage karne ke liye ViewModel
    public class UserEditMVCVM
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public bool EnableOTP { get; set; }

        // Module Permissions List
        public List<ModulePermissionEditSelection> ModulePermissions { get; set; } = new List<ModulePermissionEditSelection>();
    }

    public class ModulePermissionEditSelection
    {
        public int ModuleId { get; set; }
        public string ModuleName { get; set; }
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
