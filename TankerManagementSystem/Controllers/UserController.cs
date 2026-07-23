using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.ViewModels;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;

        public UserController(UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        // 1. FETCH ALL USERS (List View)
        [HttpGet]
        public async Task<IActionResult> FetchUsers()
        {
            var users = await _userManager.Users.Where(x => x.Id != "100").ToListAsync();
            return View(users);
        }

        // 2. CREATE USER (GET)
        [HttpGet]
        public IActionResult CreatUser()
        {
            return View(new UserCreateMVCVM());
        }

        // 3. CREATE USER (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatUser(UserCreateMVCVM vm)
        {
            //if (!ModelState.IsValid) return View(vm);

            var userExist = await _userManager.FindByEmailAsync(vm.Email);
            if (userExist != null)
            {
                TempData["Error"] = "User already exists!";
                return View(vm);
            }

            var usernameExist = await _userManager.FindByNameAsync(vm.Username);
            if (usernameExist != null)
            {
                TempData["Error"] = "Username is already taken!";
                return View(vm);
            }

            if (!await _roleManager.RoleExistsAsync(vm.SelectedRole))
            {
                TempData["Error"] = "Selected Role does not exist.";
                return View(vm);
            }

            IdentityUser user = new()
            {
                Email = vm.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = vm.Username,
                TwoFactorEnabled = vm.EnableOTP
            };

            var result = await _userManager.CreateAsync(user, vm.Password);
            if (!result.Succeeded)
            {
                TempData["Error"] = "User Failed to Create: " + result.Errors.FirstOrDefault()?.Description;
                return View(vm);
            }

            // Add Role
            await _userManager.AddToRoleAsync(user, vm.SelectedRole);

            // ==========================================
            // BY DEFAULT PERMISSION LOGIC START
            // ==========================================
            try
            {
                var personalKhataModule = await _db.AppModules
                    .FirstOrDefaultAsync(m => m.Controller.ToLower() == "personalkhata");

                if (personalKhataModule != null)
                {
                    var defaultPermission = new UserPermission
                    {
                        UserId = user.Id,
                        ModuleId = personalKhataModule.Id,
                        CanView = true,
                        CanCreate = true,
                        CanEdit = true,
                        CanDelete = true
                    };

                    _db.UserPermissions.Add(defaultPermission);
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // Modified by AI
                // Date: 2026-07-21
                // Reason: M-06 — 'throw ex' was resetting the stack trace, making
                // debugging impossible. Bare 'throw' re-throws the original exception
                // with the full stack trace preserved.
                _ = ex; // suppress "variable unused" warning
                throw;
            }
            // ==========================================
            // BY DEFAULT PERMISSION LOGIC END
            // ==========================================

            TempData["Success"] = "User created successfully.";
            return RedirectToAction("FetchUsers");
        }

        // 4. EDIT USER & MODULE ACCESS (GET)
        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var vm = new UserEditMVCVM
            {
                UserId = user.Id,
                Email = user.Email,
                Username = user.UserName,
                EnableOTP = user.TwoFactorEnabled
            };

            var allModules = await _db.AppModules.ToListAsync();
            var userPermissions = await _db.UserPermissions.Where(x => x.UserId == id).ToListAsync();

            foreach (var mod in allModules)
            {
                var existingPerm = userPermissions.FirstOrDefault(x => x.ModuleId == mod.Id);

                vm.ModulePermissions.Add(new ModulePermissionEditSelection
                {
                    ModuleId = mod.Id,
                    ModuleName = mod.ModuleName,
                    CanView = existingPerm?.CanView ?? false,
                    CanCreate = existingPerm?.CanCreate ?? false,
                    CanEdit = existingPerm?.CanEdit ?? false,
                    CanDelete = existingPerm?.CanDelete ?? false
                });
            }

            return View(vm);
        }

        // 5. EDIT USER & MODULE ACCESS (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(UserEditMVCVM vm)
        {
            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null) return NotFound();

            user.Email = vm.Email;
            user.UserName = vm.Username;
            user.TwoFactorEnabled = vm.EnableOTP;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                TempData["Error"] = "Failed to update user identity properties.";
                return View(vm);
            }

            var oldPermissions = _db.UserPermissions.Where(x => x.UserId == user.Id);
            _db.UserPermissions.RemoveRange(oldPermissions);

            if (vm.ModulePermissions != null)
            {
                foreach (var perm in vm.ModulePermissions)
                {
                    if (perm.CanView || perm.CanCreate || perm.CanEdit || perm.CanDelete)
                    {
                        var userPerm = new UserPermission
                        {
                            UserId = user.Id,
                            ModuleId = perm.ModuleId,
                            CanView = perm.CanView,
                            CanCreate = perm.CanCreate,
                            CanEdit = perm.CanEdit,
                            CanDelete = perm.CanDelete
                        };
                        _db.UserPermissions.Add(userPerm);
                    }
                }
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "User and Module Access updated successfully.";
            return RedirectToAction("FetchUsers");
        }

        // 6. CHANGE USER PASSWORD (GET)
        [HttpGet]
        public async Task<IActionResult> ChangePassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("FetchUsers");
            }

            var vm = new ChangePasswordAdminVM
            {
                UserId = user.Id,
                Username = user.UserName,
                Email = user.Email
            };

            return View(vm);
        }

        // 7. CHANGE USER PASSWORD (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordAdminVM vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("FetchUsers");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, vm.NewPassword);

            if (result.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);
                TempData["Success"] = $"Password for user '{user.UserName}' has been changed successfully.";
                return RedirectToAction("FetchUsers");
            }

            TempData["Error"] = "Failed to change password: " + result.Errors.FirstOrDefault()?.Description;
            return View(vm);
        }

        // 8. DELETE USER (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "User ID is missing.";
                return RedirectToAction("FetchUsers");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("FetchUsers");
            }

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["Success"] = $"User '{user.UserName}' has been deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Failed to delete user: " + result.Errors.FirstOrDefault()?.Description;
            }

            return RedirectToAction("FetchUsers");
        }
    }
}