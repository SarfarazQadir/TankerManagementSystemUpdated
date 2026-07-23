using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TankerManagementSystem.Models;
using TankerManagementSystem.Services;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Admin")]
    public class TankerController : Controller
    {
        private readonly ApplicationDbContext _dbcontext;

        // Modified by AI
        // Date: 2026-07-21
        // Reason: H-05 — Injecting ILedgerRecalculationService replaces the private
        // RecalculateTankerLedger() method that was duplicated in 4 controllers.
        private readonly ILedgerRecalculationService _recalcService;

        public TankerController(ApplicationDbContext dbcontext, ILedgerRecalculationService recalcService)
        {
            _dbcontext = dbcontext;
            _recalcService = recalcService;
        }

        // LIST
        public IActionResult Index()
        {
            var tankers = _dbcontext.Tankers
                .Include(x => x.Owner)
                .OrderByDescending(x => x.Id)
                .ToList();

            return View(tankers);
        }

        // ADD GET
        public IActionResult Add()
        {
            ViewBag.Owners = _dbcontext.TankerOwners.ToList();
            return View();
        }

        // ADD POST        
        [HttpPost]
        public IActionResult Add(Tanker request)
        {
            bool tankerExists = _dbcontext.Tankers.Any(x => x.TankerNo == request.TankerNo);
            if (tankerExists)
            {
                TempData["Error"] = "Tanker already exists!";
                return RedirectToAction("Add");
            }

            var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);
            request.CreatedAt = pakTime;

            var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User?.FindFirst(ClaimTypes.Name)?.Value
                                ?? User?.Identity?.Name;

            if (string.IsNullOrEmpty(currentUserId) || !(User?.Identity?.IsAuthenticated ?? false))
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login", "Admin");
            }
            request.CreatedBy = currentUserId;
            // 🔥 FIX: agar user ne OpeningBalanceDate nahi diya (default DateTime), to aaj ki date fallback
            if (request.OpeningBalanceDate == default)
            {
                request.OpeningBalanceDate = pakTime.Date;
            }

            if (string.IsNullOrWhiteSpace(request.TankerNo))
            {
                TempData["Error"] = "Tanker Number is required";
                return RedirectToAction("Add");
            }

            _dbcontext.Tankers.Add(request);
            _dbcontext.SaveChanges();

            // Tanker Creation ke waqt TransactionDate = "abhi" hi sahi hai (kyunke ye pehli row hai, backdating ka sawal nahi)
            if (request.CurrentBalance != 0)
            {
                TankerLedger ledgerLog = new TankerLedger()
                {
                    TankerId = request.Id,
                    TransactionDate = request.OpeningBalanceDate, // 🔥 FIX: pakTime ki jagah user-specified date
                    ModuleName = "Tanker Creation",
                    ReferenceId = request.Id,
                    Credit = request.CurrentBalance > 0 ? request.CurrentBalance : 0,
                    Debit = request.CurrentBalance < 0 ? Math.Abs(request.CurrentBalance) : 0,
                    RunningBalance = request.CurrentBalance,
                    Description = $"Opening balance set during tanker creation. Tanker No: {request.TankerNo}",
                    CreatedAt = pakTime,
                    CreatedBy = request.CreatedBy
                };

                _dbcontext.TankerLedgers.Add(ledgerLog);
                _dbcontext.SaveChanges();

                // Modified by AI
                // Date: 2026-07-21
                // Reason: H-05 — Using shared service instead of private method.
                _recalcService.RecalculateTankerLedger(request.Id);
            }

            TempData["add_tanker_message"] = "Tanker added successfully.";
            return RedirectToAction("Index");
        }

        // EDIT GET
        public IActionResult Edit(int id)
        {
            var tanker = _dbcontext.Tankers.FirstOrDefault(x => x.Id == id);
            if (tanker == null) return NotFound();

            ViewBag.Owners = _dbcontext.TankerOwners.ToList();
            return View(tanker);
        }

        // EDIT POST        
        [HttpPost]
        public IActionResult Edit(Tanker updateTanker)
        {
            var tanker = _dbcontext.Tankers.FirstOrDefault(x => x.Id == updateTanker.Id);
            if (tanker == null)
                return NotFound();

            var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);
            tanker.UpdatedAt = pakTime;

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
            tanker.UpdatedBy = currentUserId;

            if (string.IsNullOrWhiteSpace(updateTanker.TankerNo))
            {
                TempData["Error"] = "Tanker Number is required";
                return RedirectToAction("Edit", new { id = updateTanker.Id });
            }

            // FIX Issue 10: Only update profile fields, NOT CurrentBalance - Done by AntiGravity on 2026-07-18 08:15 PST
            // Balance is managed solely through ledger entries (trips, cash ledger, ATS/PSO)
            // to prevent concurrency race conditions from stale form data.
            tanker.TankerNo = updateTanker.TankerNo;
            tanker.OwnerId = updateTanker.OwnerId;
            tanker.Model = updateTanker.Model;
            tanker.Capacity = updateTanker.Capacity;

            _dbcontext.SaveChanges();

            TempData["edit_tanker_message"] = "Tanker updated successfully.";
            return RedirectToAction("Index");
        }

        // Modified by AI
        // Date: 2026-07-21
        // Reason: L-02 — [ActionName] corrects the double-o typo in the method name.
        // The URL /Tanker/TankerBalanceHistory now works (correct spelling).
        // L-01 — Removed the commented-out old version of this method above.
        // The view file is still named TankerBalanceHistoory.cshtml and will be served correctly.
        //[ActionName("TankerBalanceHistory")]
        public IActionResult TankerBalanceHistoory(int id, DateTime? startDate, DateTime? endDate)
        {
            var tanker = _dbcontext.Tankers.FirstOrDefault(x => x.Id == id);
            if (tanker == null) return NotFound();

            ViewBag.Owners = _dbcontext.TankerOwners.ToList();

            var query = _dbcontext.TankerLedgers.Where(x => x.TankerId == id);

            if (startDate.HasValue)
                query = query.Where(x => x.TransactionDate.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(x => x.TransactionDate.Date <= endDate.Value.Date);

            var history = query
                .OrderBy(x => x.TransactionDate)
                .ThenBy(x => x.Id)
                .ToList();

            // Opening balance = jo balance range shuru hone se pehle wala tha
            // (isi se pata chalega "1 Jan 2025 ko balance kitna tha")
            decimal openingBalance = 0;
            if (startDate.HasValue)
            {
                openingBalance = _dbcontext.TankerLedgers
                    .Where(x => x.TankerId == id && x.TransactionDate.Date < startDate.Value.Date)
                    .OrderByDescending(x => x.TransactionDate)
                    .ThenByDescending(x => x.Id)
                    .Select(x => x.RunningBalance)
                    .FirstOrDefault();
            }

            ViewBag.LedgerHistory = history;
            ViewBag.OpeningBalance = openingBalance;
            ViewBag.HasStartFilter = startDate.HasValue;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(tanker);
        }
    }
}