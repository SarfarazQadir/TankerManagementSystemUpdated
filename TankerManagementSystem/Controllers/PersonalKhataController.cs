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
    public class PersonalKhataController : Controller
    {
        private readonly ApplicationDbContext _db;

        public PersonalKhataController(ApplicationDbContext db)
        {
            _db = db;
        }

        // =========================
        // INDEX
        // =========================
        public IActionResult Index()
        {
            // 1. JWT COOKIE SE USER ID NIKALNA
            string currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User?.FindFirst(ClaimTypes.Name)?.Value
                               ?? User?.FindFirst("sub")?.Value
                               ?? User?.FindFirst(ClaimTypes.Email)?.Value
                               ?? User?.Identity?.Name;

            if (string.IsNullOrEmpty(currentUserId) || !(User?.Identity?.IsAuthenticated ?? false))
            {
                TempData["Error"] = "Session expired or invalid token. Please login again.";
                return RedirectToAction("Login", "Admin");
            }

            var data = _db.PersonalKhatas
                .OrderByDescending(x => x.Id)
                .ToList();

            return View(data);
        }

        // =========================
        // ADMIN ALL RECORDS
        // =========================
        public IActionResult AllKhatas()
        {
            var data = _db.PersonalKhatas
                .OrderByDescending(x => x.Id)
                .ToList();

            return View(data);
        }

        // =========================
        // ADD GET
        // =========================
        [HttpGet]
        public IActionResult Add()
        {
            return View();
        }

        // =========================
        // ADD POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(PersonalKhata model)
        {
            // 1. JWT COOKIE SE USER ID NIKALNA
            // =========================================
            // Agar user logged in nahi hai, toh User.FindFirst null dega
            var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User?.FindFirst(ClaimTypes.Name)?.Value
                                ?? User?.FindFirst("sub")?.Value
                                ?? User?.FindFirst(ClaimTypes.Email)?.Value
                                ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User?.Identity?.Name;

            if (string.IsNullOrEmpty(currentUserId))
            {
                TempData["Error"] = "Session expired or invalid token. Please login again.";
                return RedirectToAction("Login", "Admin");
            }

            string userId = currentUserId;

            var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");

            DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            model.KhataPerson.Name = userId;

            model.CreatedAt = pakTime;

            model.CreatedBy = currentUserId;

            // =========================
            // LAST BALANCE
            // =========================
            decimal lastBalance = _db.PersonalKhatas
                .Where(x => x.KhataPerson.Name == userId)
                .OrderByDescending(x => x.Id)
                .Select(x => x.Balance)
                .FirstOrDefault();

            // =========================
            // BALANCE CALCULATION
            // =========================
            model.Balance =
                lastBalance
                + model.AddAmount
                - model.MinusAmount;

            _db.PersonalKhatas.Add(model);

            _db.SaveChanges();

            TempData["success"] = "Customer Credit Entry Added";

            return RedirectToAction("Index");
        }

        // =========================
        // EDIT GET
        // =========================
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var data = _db.PersonalKhatas.Find(id);

            if (data == null)
            {
                return NotFound();
            }

            return View(data);
        }

        // =========================
        // EDIT POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(PersonalKhata model)
        {
            var data = _db.PersonalKhatas.Find(model.Id);

            if (data == null)
            {
                return NotFound();
            }

            data.EntryDate = model.EntryDate;
            data.Description = model.Description;
            data.AddAmount = model.AddAmount;
            data.MinusAmount = model.MinusAmount;

            data.Balance =
                model.AddAmount - model.MinusAmount;

            data.UpdatedAt = DateTime.Now;

            _db.SaveChanges();

            TempData["success"] = "Record Updated";

            return RedirectToAction("Index");
        }

        // =========================
        // DELETE
        // =========================
        public IActionResult Delete(int id)
        {
            var data = _db.PersonalKhatas.Find(id);

            if (data == null)
            {
                return NotFound();
            }

            _db.PersonalKhatas.Remove(data);

            _db.SaveChanges();

            TempData["success"] = "Record Deleted";

            return RedirectToAction("Index");
        }

        // =========================
        // PRINT
        // =========================
        public IActionResult Print()
        {
            // 1. JWT COOKIE SE USER ID NIKALNA
            string currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User?.FindFirst(ClaimTypes.Name)?.Value
                               ?? User?.FindFirst("sub")?.Value
                               ?? User?.FindFirst(ClaimTypes.Email)?.Value
                               ?? User?.Identity?.Name;

            if (string.IsNullOrEmpty(currentUserId) || !(User?.Identity?.IsAuthenticated ?? false))
            {
                TempData["Error"] = "Session expired or invalid token. Please login again.";
                return RedirectToAction("Login", "Admin");
            }

            string userId = currentUserId;

            // Data ko descending order (Latest Date) me load kiya
            var data = _db.PersonalKhatas
                .Where(x => x.KhataPerson.Name == userId)
                .OrderByDescending(x => x.EntryDate)
                .ToList();

            return View(data);
        }

    }
}