// Modified by AI
// Date: 2026-07-21
// Reason: M-05 — UserManager and SignInManager are now injected via the constructor
// instead of being resolved via HttpContext.RequestServices.GetService (Service Locator
// anti-pattern). This makes dependencies explicit, testable, and correctly scoped.
// The unnecessary null check on userManager is also removed — if DI is misconfigured,
// the app fails at startup, not silently at runtime.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace TankerManagementSystem.Controllers
{
    public class AdminController : Controller
    {
        // Modified by AI
        // Date: 2026-07-21
        // Reason: M-05 — Injected via constructor instead of Service Locator pattern.
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AdminController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
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

            // Modified by AI
            // Date: 2026-07-21
            // Reason: M-05 — Use injected _userManager and _signInManager instead of
            // HttpContext.RequestServices.GetService (Service Locator anti-pattern).

            // Get the current Identity user
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Profile");
            }

            // Verify current password before attempting change
            var passwordValid = await _userManager.CheckPasswordAsync(user, currentPassword);
            if (!passwordValid)
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction("Profile");
            }

            // Change password using ASP.NET Identity (updates AspNetUsers table)
            var changeResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (changeResult.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
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
