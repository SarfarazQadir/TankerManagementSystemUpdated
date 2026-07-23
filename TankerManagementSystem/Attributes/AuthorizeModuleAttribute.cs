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

            // Modified by AI
            // Date: 2026-07-21
            // Reason: H-08 — All action name string literals must be lowercase to match
            // currentAction which is already lowercased via .ToLower() on line 37.
            // Previously "EditLedger" (PascalCase) never matched "editledger" (lowercase),
            // causing TripLedger's EditLedger action to fall into the else branch and
            // check CanView instead of CanEdit — effectively bypassing edit authorization.
            // Also added "addoldentry", "allreport", "generate", "printbill",
            // "statement", "allkhatas", "print", "monthlyreport", "dailyreport",
            // "allreport", "profitloss", "expenseanalysis" to correct permission mapping.

            // VIEW actions
            if (currentAction == "index"
                || currentAction == "details"
                || currentAction == "fetchusers"
                || currentAction == "getall"
                || currentAction == "allreport"
                || currentAction == "dailyreport"
                || currentAction == "monthlyreport"
                || currentAction == "profitloss"
                || currentAction == "expenseanalysis"
                || currentAction == "generate"
                || currentAction == "printbill"
                || currentAction == "statement"
                || currentAction == "allkhatas"
                || currentAction == "print"
                || currentAction == "tankerbalancehistory")
            {
                isAllowed = permission.CanView;
            }
            // CREATE actions
            else if (currentAction == "create"
                     || currentAction == "add"
                     || currentAction == "save"
                     || currentAction == "insert"
                     || currentAction == "addentry"
                     || currentAction == "addledger"
                     || currentAction == "addoldentry")
            {
                isAllowed = permission.CanCreate;
            }
            // EDIT actions
            else if (currentAction == "edit"
                     || currentAction == "update"
                     || currentAction == "modify"
                     || currentAction == "editentry"
                     || currentAction == "editledger")  // was "EditLedger" (wrong case) — fixed
            {
                isAllowed = permission.CanEdit;
            }
            // DELETE actions
            else if (currentAction == "delete"
                     || currentAction == "remove"
                     || currentAction == "destroy"
                     || currentAction == "deleteentry"
                     || currentAction == "deleteuser")
            {
                isAllowed = permission.CanDelete;
            }
            else
            {
                // Unknown action — default to CanView as safe fallback.
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