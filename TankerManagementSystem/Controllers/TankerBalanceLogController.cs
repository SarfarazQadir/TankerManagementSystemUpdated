using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Models;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AuthorizeModule]
    public class TankerBalanceLogController : Controller
    {
        private readonly ApplicationDbContext _db;

        public TankerBalanceLogController(ApplicationDbContext db)
        {
            _db = db;
        }

        // =========================
        // ALL LOGS
        // =========================
        public IActionResult Index()
        {
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
            var data = _db.TankerBalanceLogs
                .Include(x => x.Tanker)
                .OrderByDescending(x => x.Id)
                .ToList();

            return View(data);
        }

        // =========================
        // TANKER WISE LOGS
        // =========================
        public IActionResult TankerLogs(int tankerId)
        {
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
            var data = _db.TankerBalanceLogs
                .Include(x => x.Tanker)
                .Where(x => x.TankerId == tankerId)
                .OrderByDescending(x => x.Id)
                .ToList();

            ViewBag.Tanker = _db.Tankers
                .FirstOrDefault(x => x.Id == tankerId);

            return View(data);
        }
    }
}