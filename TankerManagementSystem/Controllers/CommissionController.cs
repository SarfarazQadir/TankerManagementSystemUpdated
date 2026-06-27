using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TankerManagementSystem.Attributes;
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
            var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            request.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);

            var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                        ?? User?.FindFirst(ClaimTypes.Name)?.Value
                                        ?? User?.FindFirst("sub")?.Value
                                        ?? User?.FindFirst(ClaimTypes.Email)?.Value
                                        ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
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

            var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            data.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);

            data.Name = update.Name;
            data.Percentage = update.Percentage;
            data.IsActive = update.IsActive;

            var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                            ?? User?.FindFirst(ClaimTypes.Name)?.Value
                                            ?? User?.FindFirst("sub")?.Value
                                            ?? User?.FindFirst(ClaimTypes.Email)?.Value
                                            ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
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