/*using Microsoft.AspNetCore.Authentication.JwtBearer;
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
            // 1. Check user authentication under JWT scheme
            var user = context.HttpContext.User;
            if (user == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new ChallengeResult(JwtBearerDefaults.AuthenticationScheme);
                return;
            }

            // 2. Get UserId from Claims
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? user.FindFirst(ClaimTypes.Name)?.Value; 

            if (string.IsNullOrEmpty(userId))
            {
                RedirectToAccessDenied(context, "User context not found.");
                return;
            }

            // 3. Get Route Data (Controller and Action)
            var routeData = context.RouteData.Values;
            string currentController = routeData["controller"]?.ToString();
            string currentAction = routeData["action"]?.ToString();

            // 4. Resolve Database Context dynamically
            var dbContext = context.HttpContext.RequestServices.GetService<ApplicationDbContext>();
            if (dbContext == null)
            {
                RedirectToAccessDenied(context, "Database service unavailable.");
                return;
            }

            // 5. Query Database for Permission
            var hasPermission = await dbContext.UserPermissions
                .Include(up => up.Module)
                .AnyAsync(up =>
                    (up.UserId == userId || up.User.UserName == userId) && // ID ya Username dono handle karne k liye
                    up.Module.Controller.ToLower() == currentController.ToLower() &&
                    up.Module.Action.ToLower() == currentAction.ToLower() &&
                    up.CanView == true); // View permission check

            // 6. If no permission, redirect
            if (!hasPermission)
            {
                RedirectToAccessDenied(context, $"Aapko '{currentController}/{currentAction}' module dekhne ki ijazat nahi hai.");
            }
        }

        private void RedirectToAccessDenied(AuthorizationFilterContext context, string message)
        {
            // TempData ya QueryString k zariye message pass karna
            var controller = context.RouteData.Values["controller"]?.ToString();

            // Redirect to a specific route/page
            context.Result = new RedirectToActionResult("AccessDenied", "Authentication", new { message = message });
        }
    }
}*/
/*using Microsoft.AspNetCore.Authentication.JwtBearer;
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
            // 1. Verify user authentication under the JWT Bearer scheme
            var user = context.HttpContext.User;
            if (user == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new ChallengeResult(JwtBearerDefaults.AuthenticationScheme);
                return;
            }

            // If the user belongs to the "Admin" role, bypass all module permission checks
            if (user.IsInRole("Admin"))
            {
                return; // Grant full access immediately
            }

            // 2. Extract Identity context from claims for non-admin roles (e.g., "User")
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? user.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                RedirectToAccessDenied(context, "Identity context cannot be established.");
                return;
            }

            // 3. Extract target route data metrics
            var routeData = context.RouteData.Values;
            string currentController = routeData["controller"]?.ToString();
            string currentAction = routeData["action"]?.ToString();

            // 4. Resolve the shared infrastructure database context
            var dbContext = context.HttpContext.RequestServices.GetService<ApplicationDbContext>();
            if (dbContext == null)
            {
                RedirectToAccessDenied(context, "System infrastructure error: Database connection unavailable.");
                return;
            }

            // 5. Query relational module permissions for the target user context
            var hasPermission = await dbContext.UserPermissions
                .Include(up => up.Module)
                .AnyAsync(up =>
                    (up.UserId == userId || up.User.UserName == userId) &&
                    up.Module.Controller.ToLower() == currentController.ToLower() &&
                    up.Module.Action.ToLower() == currentAction.ToLower() &&
                    up.CanView);

            // 6. Restrict access if permission matrix is unverified
            if (!hasPermission)
            {
                RedirectToAccessDenied(context, $"Access Denied: You do not have view privileges for the '{currentController}' operational module.");
            }
        }

        private void RedirectToAccessDenied(AuthorizationFilterContext context, string message)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", new { message = message });
        }
    }
}*/
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