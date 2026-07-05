using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TankerManagementSystem.Models;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Admin")]
    public class TankerController : Controller
    {
        private readonly ApplicationDbContext _dbcontext;

        public TankerController(ApplicationDbContext dbcontext)
        {
            _dbcontext = dbcontext;
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

            if (string.IsNullOrWhiteSpace(request.TankerNo))
            {
                TempData["Error"] = "Tanker Number is required";
                return RedirectToAction("Add");
            }

            // =========================================================
            // CHANGE 1: Column name changed to CurrentBalance
            // =========================================================
            // Yahan hum assume kar rahe hain ke opening balance agar user ne dala hai to wo 'Payable' (Profit) hai, isliye sign (+) hai.
            request.CurrentBalance = request.CurrentBalance;

            _dbcontext.Tankers.Add(request);
            _dbcontext.SaveChanges();

            // =========================================================
            // CHANGE 2: Naye Professional Ledger ke mutabiq log save hoga
            // =========================================================
            if (request.CurrentBalance != 0)
            {
                TankerLedger ledgerLog = new TankerLedger()
                {
                    TankerId = request.Id,
                    TransactionDate = pakTime,
                    ModuleName = "Tanker Creation",
                    ReferenceId = request.Id,

                    // Agar balance positive hai toh Credit (Company ne dena hai), agar negative hai toh Debit (Owner ne dena hai)
                    Credit = request.CurrentBalance > 0 ? request.CurrentBalance : 0,
                    Debit = request.CurrentBalance < 0 ? Math.Abs(request.CurrentBalance) : 0,

                    RunningBalance = request.CurrentBalance,
                    Description = $"Opening balance set during tanker creation. Tanker No: {request.TankerNo}",
                    CreatedAt = pakTime,
                    CreatedBy = request.CreatedBy
                };

                _dbcontext.TankerLedgers.Add(ledgerLog); // Naya table name
                _dbcontext.SaveChanges();
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
            // =========================
            // FETCH TANKER
            // =========================
            var tanker = _dbcontext.Tankers
                .FirstOrDefault(x => x.Id == updateTanker.Id);

            if (tanker == null)
                return NotFound();

            // =========================
            // PAKISTAN TIME
            // =========================
            var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);
            tanker.UpdatedAt = pakTime;

            // =========================
            // SESSION
            // =========================
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

            // =========================
            // VALIDATION
            // =========================
            if (string.IsNullOrWhiteSpace(updateTanker.TankerNo))
            {
                TempData["Error"] = "Tanker Number is required";
                return RedirectToAction("Edit", new { id = updateTanker.Id });
            }

            // =========================================================
            // CHANGE 1: Column name changed from PreviousBalance to CurrentBalance
            // =========================================================
            decimal oldBalance = tanker.CurrentBalance;

            // =========================
            // UPDATE DATA
            // =========================
            tanker.TankerNo = updateTanker.TankerNo;
            tanker.OwnerId = updateTanker.OwnerId;
            tanker.Model = updateTanker.Model;
            tanker.Capacity = updateTanker.Capacity;

            // Assigning the new balance to the updated column name
            tanker.CurrentBalance = updateTanker.CurrentBalance;

            decimal newBalance = updateTanker.CurrentBalance;

            // =========================
            // SAVE TANKER
            // =========================
            _dbcontext.SaveChanges();

            // =========================================================
            // CHANGE 2: Professional Accounting Ledger Entry
            // =========================================================
            decimal difference = newBalance - oldBalance;

            if (difference != 0)
            {
                // Sign mapping for Accounting:
                // Difference > 0: Balance barh gaya (Credit - Company owes more money to Owner)
                // Difference < 0: Balance kam ho gaya (Debit - Company deducted money or Owner owes company)
                decimal creditAmount = difference > 0 ? difference : 0;
                decimal debitAmount = difference < 0 ? Math.Abs(difference) : 0;

                string description = difference > 0
                    ? $"Balance manually increased via Tanker Edit. Tanker No: {tanker.TankerNo}"
                    : $"Balance manually decreased via Tanker Edit. Tanker No: {tanker.TankerNo}";

                // Using the new TankerLedger model instead of the old TankerBalanceLog
                TankerLedger ledgerLog = new TankerLedger()
                {
                    TankerId = tanker.Id,
                    TransactionDate = pakTime,
                    ModuleName = "Tanker Edit",
                    ReferenceId = tanker.Id,

                    Credit = creditAmount,
                    Debit = debitAmount,
                    RunningBalance = newBalance, // Net financial position after adjustment

                    Description = description,
                    CreatedAt = pakTime,
                    CreatedBy = tanker.UpdatedBy
                };

                _dbcontext.TankerLedgers.Add(ledgerLog);
                _dbcontext.SaveChanges();
            }

            // =========================
            // SUCCESS
            // =========================
            TempData["edit_tanker_message"] = "Tanker updated successfully.";
            return RedirectToAction("Index");
        }
    }
}