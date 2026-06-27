using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Engines;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.Email;
using TankerManagementSystem.Models.ViewModels;
using IEmailService = TankerManagementSystem.Services.IEmailService;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _emailService;
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;

        public UserController(UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext db,
            IEmailService emailService, IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _emailService = emailService;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
        }

        // 1. FETCH ALL USERS (List View)
        [HttpGet]
        public async Task<IActionResult> FetchUsers()
        {
            var users = await _userManager.Users.Where(x=>x.Id != "100").ToListAsync();
            return View(users);
        }

        // 2. CREATE USER (GET)
        [HttpGet]
        public IActionResult CreatUser()
        {
            return View(new UserCreateMVCVM());
        }

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

            // Default IdentityUser object
            IdentityUser user = new()
            {
                Email = vm.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = vm.Username,
                TwoFactorEnabled = vm.EnableOTP // OTP configuration yahan se mapping hogi
            };

            if (await _roleManager.RoleExistsAsync(vm.SelectedRole))
            {
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
                    // 1. AppModule table se PersonalKhata ka module dhoondein (Controller name base par)
                    var personalKhataModule = await _db.AppModules
                        .FirstOrDefaultAsync(m => m.Controller.ToLower() == "personalkhata");

                    if (personalKhataModule != null)
                    {
                        // 2. Naya permission object banayein jisme saare access true hon
                        var defaultPermission = new UserPermission
                        {
                            UserId = user.Id, // Naye bane hue user ki Id
                            ModuleId = personalKhataModule.Id,
                            CanView = true,
                            CanCreate = true,
                            CanEdit = true,
                            CanDelete = true
                        };

                        // 3. Database me add kar ke save karein
                        _db.UserPermissions.Add(defaultPermission);
                        await _db.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                // ==========================================
                // BY DEFAULT PERMISSION LOGIC END
                // ==========================================

                // ==========================================
                // NEW CODE: HTML EMAIL VIEW RENDERING START
                // ==========================================

                // 1. Generate Email Confirmation Token and Link
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = Url.Action("ConfirmEmail", "Authentication", new { token, email = user.Email }, Request.Scheme);
                //var logoUrl = $"{Request.Scheme}://{Request.Host}/img/flyeasy.png";
                var logoUrl = $"http://malikbrother.runasp.net/img/flyeasy.png";

                // 2. Dynamic ExpandoObject banana taake View Template me data pass ho sake
                var emailData = new System.Dynamic.ExpandoObject() as dynamic;
                emailData.Username = user.UserName;
                emailData.ConfirmationLink = confirmationLink;
                emailData.LogoUrl = logoUrl;

                // 3. View ko string me render karna (Same like your API logic)
                string emailBody = await RenderViewToStringAsync("~/Views/Shared/_EmailConfirmationTemplate.cshtml", emailData);

                // 4. HTML Email Send karna
                var message = new Message(new string[] { user.Email! }, "Tanker Management System Account Verification Link", emailBody);
                _emailService.SendEmail(message);

                // ==========================================
                // NEW CODE: HTML EMAIL VIEW RENDERING END
                // ==========================================

                TempData["Success"] = $"User created & Email Sent to {user.Email} Successfully.";
                return RedirectToAction("FetchUsers");
            }
            else
            {
                TempData["Error"] = "Selected Role does not exist.";
                return View(vm);
            }
        }

        private async Task<string> RenderViewToStringAsync(string viewPath, object model)
        {
            // Fix 1: Removed typo and properly initialized ActionContext
            var actionContext = new ActionContext(HttpContext, RouteData, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

            using (var sw = new StringWriter())
            {
                // Fix 2: Corrected GetView parameters (removed viewExecutingEnv)
                var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);

                // Fix 3: Changed .Succeeded to .Success
                if (!viewResult.Success)
                {
                    viewResult = _viewEngine.FindView(actionContext, viewPath, isMainPage: false);
                }

                if (!viewResult.Success)
                {
                    throw new ArgumentNullException($"Template view file could not be located at path: {viewPath}");
                }

                var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                {
                    Model = model
                };

                var viewContext = new ViewContext(
                    actionContext,
                    viewResult.View,
                    viewDictionary,
                    new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                    sw,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                return sw.ToString();
            }
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

            // Database se saare modules aur is user ki existing permissions nikalna
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

            // 1. Basic properties update
            user.Email = vm.Email;
            user.UserName = vm.Username;
            user.TwoFactorEnabled = vm.EnableOTP; // OTP setting edit page se save hogi

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                TempData["Error"] = "Failed to update user identity properties.";
                return View(vm);
            }

            // 2. Module Permissions Update
            // Pehle purani saari permissions remove karein is user ki
            var oldPermissions = _db.UserPermissions.Where(x => x.UserId == user.Id);
            _db.UserPermissions.RemoveRange(oldPermissions);

            // Nayi checked permissions insert karein
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

            // Admin direct password change kar raha hai, isliye reset token generate karenge
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, vm.NewPassword);

            if (result.Succeeded)
            {
                // Password change hone par security stamp update karna zaroori hai takay purane sessions logout ho jayein
                await _userManager.UpdateSecurityStampAsync(user);

                TempData["Success"] = $"Password for user '{user.UserName}' has been changed successfully.";
                return RedirectToAction("FetchUsers");
            }

            // Agar Identity validation fail ho jaye (e.g. password policy issue)
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

            // User ko delete karne ka process
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