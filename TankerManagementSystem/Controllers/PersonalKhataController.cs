// Modified by AI
// Date: 2026-07-21
// Reason: H-05 — Replaced private RecalculatePersonalKhata() with ILedgerRecalculationService injection.
//         C-05 — Removed redundant manual Balance pre-computation before RecalculatePersonalKhata().

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Helpers;
using TankerManagementSystem.Models;
using TankerManagementSystem.Services;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AuthorizeModule]
    public class PersonalKhataController : Controller
    {
        private readonly ApplicationDbContext _db;

        // Modified by AI
        // Date: 2026-07-21
        // Reason: H-05 — Injecting ILedgerRecalculationService replaces the private
        // RecalculatePersonalKhata() method that was duplicated in multiple places.
        // C-05 — The shared service call in Add() is now the single authoritative
        // balance setter, removing the intermediate redundant saves.
        private readonly ILedgerRecalculationService _recalcService;

        public PersonalKhataController(ApplicationDbContext db, ILedgerRecalculationService recalcService)
        {
            _db = db;
            _recalcService = recalcService;
        }

        /// <summary>
        /// Retrieves the current user ID from JWT claims.
        /// Returns null if user is not authenticated.
        /// </summary>
        private string? GetCurrentUserId()
        {
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User?.FindFirst(ClaimTypes.Name)?.Value
                         ?? User?.FindFirst("sub")?.Value
                         ?? User?.FindFirst(ClaimTypes.Email)?.Value
                         ?? User?.Identity?.Name;

            if (string.IsNullOrEmpty(userId) || !(User?.Identity?.IsAuthenticated ?? false))
            {
                return null;
            }

            return userId;
        }

        // =========================
        // INDEX
        // =========================
        public IActionResult Index()
        {
            string? currentUserId = GetCurrentUserId();

            if (currentUserId == null)
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
            var currentUserId = GetCurrentUserId();

            if (currentUserId == null)
            {
                TempData["Error"] = "Session expired or invalid token. Please login again.";
                return RedirectToAction("Login", "Admin");
            }

            var pakTime = DateTimeHelper.GetPakistanTime();

            // FIX Issue 02: Find or create the KhataPerson record for the current user
            var person = _db.KhataPersons.FirstOrDefault(x => x.Name == currentUserId);
            if (person == null)
            {
                person = new KhataPerson
                {
                    Name = currentUserId,
                    CurrentBalance = 0,
                    CreatedAt = pakTime,
                    CreatedBy = currentUserId
                };
                _db.KhataPersons.Add(person);
                _db.SaveChanges();
            }

            model.KhataPersonId = person.Id;
            model.CreatedAt = pakTime;
            model.CreatedBy = currentUserId;

            // Modified by AI
            // Date: 2026-07-21
            // Reason: C-05 — Previously, Balance was computed manually here AND
            // person.CurrentBalance was set before RecalculatePersonalKhata(), creating
            // two redundant intermediate saves with potentially wrong values.
            // Since RecalculatePersonalKhata() always overwrites Balance and
            // person.CurrentBalance with the correct date-ordered values, the pre-computation
            // is unnecessary. Saving with Balance=0 avoids any risk of stale data
            // surviving an exception between the first and final save.
            model.Balance = 0; // Will be correctly set by RecalculatePersonalKhata below

            _db.PersonalKhatas.Add(model);
            _db.SaveChanges();

            // RecalculatePersonalKhata sets the correct date-ordered Balance on every row
            // and sets person.CurrentBalance to the final running total.
            _recalcService.RecalculatePersonalKhata(person.Id);

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
            if (data == null) return NotFound();
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

            var pakTime = DateTimeHelper.GetPakistanTime();

            data.EntryDate = model.EntryDate;
            data.Description = model.Description;
            data.AddAmount = model.AddAmount;
            data.MinusAmount = model.MinusAmount;

            // FIX Issue 11: Don't set balance manually — let RecalculatePersonalKhata fix the chain.
            data.UpdatedAt = pakTime;

            _db.SaveChanges();

            // Modified by AI
            // Date: 2026-07-21
            // Reason: H-05 — Using service instead of private method.
            _recalcService.RecalculatePersonalKhata(data.KhataPersonId);

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

            int khataPersonId = data.KhataPersonId;

            _db.PersonalKhatas.Remove(data);
            _db.SaveChanges();

            // Modified by AI
            // Date: 2026-07-21
            // Reason: H-05 — Using service instead of private method.
            // FIX Issue 12: Recalculate running balances and update KhataPerson.CurrentBalance
            _recalcService.RecalculatePersonalKhata(khataPersonId);

            TempData["success"] = "Record Deleted";
            return RedirectToAction("Index");
        }

        // =========================
        // MY LEDGER VIEW (Current user's entries)
        // =========================
        public IActionResult MyLedger()
        {
            var currentUserId = GetCurrentUserId();

            if (currentUserId == null)
            {
                TempData["Error"] = "Session expired or invalid token. Please login again.";
                return RedirectToAction("Login", "Admin");
            }

            var data = _db.PersonalKhatas
                .Where(x => x.KhataPerson.Name == currentUserId)
                .OrderByDescending(x => x.EntryDate)
                .ToList();

            return View(data);
        }

        // =========================
        // ADD OLD ENTRY (GET) — Cash Ledger ko touch nahi karta
        // =========================
        [HttpGet]
        public IActionResult AddOldEntry()
        {
            ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
            return View();
        }

        // =========================
        // ADD OLD ENTRY (POST)
        // FIX Issue 14: Purani/manual entries ka option — is se PersonalKhata me
        // direct entry ho jati hai kisi bhi selected KhataPerson ke against,
        // Cash Ledger ya Tanker Ledger ko affect kiye baghair.
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddOldEntry([Bind("EntryDate,KhataPersonId,Description,AddAmount,MinusAmount")] PersonalKhata model)
        {
            if (model.KhataPersonId <= 0)
            {
                TempData["error"] = "Pehle customer select karain.";
                ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
                return View(model);
            }

            var person = _db.KhataPersons.Find(model.KhataPersonId);
            if (person == null)
            {
                return NotFound();
            }

            var currentUserId = GetCurrentUserId() ?? User?.Identity?.Name ?? "Admin";
            var pakTime = DateTimeHelper.GetPakistanTime();

            model.CreatedAt = pakTime;
            model.CreatedBy = currentUserId;
            model.ModuleName = "Old Entry";
            model.ReferenceId = null;
            model.Balance = 0; // Will be set by RecalculatePersonalKhata below

            _db.PersonalKhatas.Add(model);
            _db.SaveChanges();

            // Modified by AI
            // Date: 2026-07-21
            // Reason: H-05 — Using service instead of private method.
            // Backdated entry may exist, so full chain recalculation (date-ordered) is needed.
            _recalcService.RecalculatePersonalKhata(person.Id);

            TempData["success"] = $"Purani entry '{person.Name}' ki ledger me add ho gai. Cash Ledger balance affect nahi hua.";
            return RedirectToAction("Statement", "KhataPerson", new { id = person.Id });
        }
    }
}