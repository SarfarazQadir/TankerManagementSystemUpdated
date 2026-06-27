using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace TankerManagementSystem.Controllers
{
    public class AdminController : Controller
    {
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] // 👈 Yeh lazmi add karein
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Login()
        {
            return View();
        }
     

        [HttpPost]
        public async Task<IActionResult> Profile()
        {
            // Ensure user is logged in via Identity
            if (!User.Identity?.IsAuthenticated ?? true) return RedirectToAction("Login", "Admin");

            // Read passwords from the posted form
            var currentPassword = Request.Form["CurrentPassword"].ToString();
            var newPassword = Request.Form["NewPassword"].ToString();
            var confirmPassword = Request.Form["ConfirmPassword"].ToString();

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["Error"] = "Current and new password must be provided.";
                return RedirectToAction("Profile");
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToAction("Profile");
            }

            // Resolve Identity services from DI
            var userManager = HttpContext.RequestServices.GetService(typeof(UserManager<IdentityUser>)) as UserManager<IdentityUser>;
            var signInManager = HttpContext.RequestServices.GetService(typeof(SignInManager<IdentityUser>)) as SignInManager<IdentityUser>;

            if (userManager == null)
            {
                TempData["Error"] = "User manager is not available.";
                return RedirectToAction("Profile");
            }

            // Get the current Identity user
            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Profile");
            }

            // Verify current password before attempting change
            var passwordValid = await userManager.CheckPasswordAsync(user, currentPassword);
            if (!passwordValid)
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction("Profile");
            }

            // Change password using ASP.NET Identity (updates AspNetUsers table)
            var changeResult = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (changeResult.Succeeded)
            {
                if (signInManager != null)
                {
                    await signInManager.RefreshSignInAsync(user);
                }
                TempData["Success"] = "Password changed successfully.";
                return RedirectToAction("Profile");
            }
            else
            {
                var errors = string.Join("; ", changeResult.Errors.Select(e => e.Description));
                TempData["Error"] = $"Failed to change password: {errors}";
                return RedirectToAction("Profile");
            }
        }
    }
}
