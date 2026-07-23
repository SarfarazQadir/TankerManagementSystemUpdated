using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Helpers;
using TankerManagementSystem.Models;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AuthorizeModule]
    public class CommissionController : Controller
    {
        private readonly ApplicationDbContext _dbcontext;

        public CommissionController(ApplicationDbContext dbcontext)
        {
            _dbcontext = dbcontext;
        }

        // LIST
        public IActionResult Index()
        {
            var data = _dbcontext.CommissionSetups
                .OrderByDescending(x => x.Id)
                .ToList();

            return View(data);
        }

        // ADD GET
        public IActionResult Add()
        {
            return View();
        }

        // ADD POST
        [HttpPost]
        public IActionResult Add(CommissionSetup request)
        {
            var pakTime = DateTimeHelper.GetPakistanTime();
            request.CreatedAt = pakTime;

            var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                        ?? User?.FindFirst(ClaimTypes.Name)?.Value
                                        ?? User?.FindFirst("sub")?.Value
                                        ?? User?.FindFirst(ClaimTypes.Email)?.Value
                                        ?? User?.Identity?.Name;

            if (string.IsNullOrEmpty(currentUserId) || !(User?.Identity?.IsAuthenticated ?? false))
            {
                TempData["Error"] = "Session expired or invalid token. Please login again.";
                return RedirectToAction("Login", "Admin");
            }

            request.CreatedBy = currentUserId;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                TempData["Error"] = "Commission Name is required";
                return RedirectToAction("Add");
            }

            // FIX Issue 14: Deactivate all other commissions when adding an active one - Done by AntiGravity on 2026-07-18 08:15 PST
            if (request.IsActive)
            {
                var otherActive = _dbcontext.CommissionSetups
                    .Where(x => x.IsActive)
                    .ToList();
                foreach (var item in otherActive)
                {
                    item.IsActive = false;
                }
            }

            _dbcontext.CommissionSetups.Add(request);
            _dbcontext.SaveChanges();

            TempData["add_commission_message"] = "Commission added successfully.";
            return RedirectToAction("Index");
        }

        // EDIT GET
        public IActionResult Edit(int id)
        {
            var data = _dbcontext.CommissionSetups.FirstOrDefault(x => x.Id == id);
            if (data == null) return NotFound();

            return View(data);
        }

        // EDIT POST
        [HttpPost]
        public IActionResult Edit(CommissionSetup update)
        {
            var data = _dbcontext.CommissionSetups.FirstOrDefault(x => x.Id == update.Id);
            if (data == null) return NotFound();

            var pakTime = DateTimeHelper.GetPakistanTime();
            data.UpdatedAt = pakTime;

            data.Name = update.Name;
            data.Percentage = update.Percentage;
            data.IsActive = update.IsActive;

            // FIX Issue 14: Deactivate all other commissions when setting this one active - Done by AntiGravity on 2026-07-18 08:15 PST
            if (update.IsActive)
            {
                var otherActive = _dbcontext.CommissionSetups
                    .Where(x => x.IsActive && x.Id != update.Id)
                    .ToList();
                foreach (var item in otherActive)
                {
                    item.IsActive = false;
                }
            }

            var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                            ?? User?.FindFirst(ClaimTypes.Name)?.Value
                                            ?? User?.FindFirst("sub")?.Value
                                            ?? User?.FindFirst(ClaimTypes.Email)?.Value
                                            ?? User?.Identity?.Name;

            if (string.IsNullOrEmpty(currentUserId) || !(User?.Identity?.IsAuthenticated ?? false))
            {
                TempData["Error"] = "Session expired or invalid token. Please login again.";
                return RedirectToAction("Login", "Admin");
            }
            data.UpdatedBy = currentUserId;

            _dbcontext.SaveChanges();

            TempData["edit_commission_message"] = "Commission updated successfully.";
            return RedirectToAction("Index");
        }
    }
}