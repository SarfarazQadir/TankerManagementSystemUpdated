using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TankerManagementSystem.Models;

namespace TankerManagementSystem.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class AuthorizeModuleAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new ChallengeResult(JwtBearerDefaults.AuthenticationScheme);
                return;
            }

            // Admin ko bypass karein (Admin ko sab allowed hai)
            if (user.IsInRole("Admin")) return;

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? user.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                RedirectToAccessDenied(context, "Identity context cannot be established.");
                return;
            }

            var routeData = context.RouteData.Values;
            string currentController = routeData["controller"]?.ToString().ToLower() ?? "";
            string currentAction = routeData["action"]?.ToString().ToLower() ?? "";

            var dbContext = context.HttpContext.RequestServices.GetService<ApplicationDbContext>();
            if (dbContext == null)
            {
                RedirectToAccessDenied(context, "Database connection unavailable.");
                return;
            }

            // Database se user ki specific permission uthayein controller ke naam par
            var permission = await dbContext.UserPermissions
                .Include(up => up.Module)
                .FirstOrDefaultAsync(up =>
                    (up.UserId == userId || up.User.UserName == userId) &&
                    up.Module.Controller.ToLower() == currentController);

            if (permission == null)
            {
                RedirectToAccessDenied(context, $"Access Denied: Is module '{currentController}' ka koi access nahi mila.");
                return;
            }

            bool isAllowed = false;

            // Sakht Action Checking Logic
            if (currentAction == "index" || currentAction == "details" || currentAction == "fetchusers" || currentAction == "getall")
            {
                isAllowed = permission.CanView;
            }
            // Agar action name 'create' ho ya 'add' ho (Dono cases handle)
            else if (currentAction == "create" || currentAction == "add" || currentAction == "save" || currentAction == "insert" || currentAction == "addentry" || currentAction == "addLedger")
            {
                isAllowed = permission.CanCreate;
            }
            else if (currentAction == "edit" || currentAction == "update" || currentAction == "modify" || currentAction == "editentry" || currentAction == "EditLedger")
            {
                isAllowed = permission.CanEdit;
            }
            else if (currentAction == "delete" || currentAction == "remove" || currentAction == "destroy")
            {
                isAllowed = permission.CanDelete;
            }
            else
            {
                // Agar koi aisa action ho jo samajh na aaye, toh hamesha strict check (False) rakhein ya CanView par choren
                isAllowed = permission.CanView;
            }

            // Agar flag false hai toh Access Block karein
            if (!isAllowed)
            {
                RedirectToAccessDenied(context, $"Access Denied: You do not have access for the '{currentController}' operational module.");
            }
        }
        private void RedirectToAccessDenied(AuthorizationFilterContext context, string message)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", new { message = message });
        }
    }
}