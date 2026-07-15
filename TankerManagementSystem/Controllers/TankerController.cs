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

        // ==========================================
        // 🔥 SHARED HELPER: Recalculate RunningBalance
        // ==========================================
        private void RecalculateTankerLedger(int tankerId)
        {
            var rows = _dbcontext.TankerLedgers
                .Where(x => x.TankerId == tankerId)
                .OrderBy(x => x.TransactionDate)
                .ThenBy(x => x.Id)
                .ToList();

            decimal running = 0;
            foreach (var row in rows)
            {
                running += (row.Credit - row.Debit);
                row.RunningBalance = running;
            }

            var tanker = _dbcontext.Tankers.FirstOrDefault(t => t.Id == tankerId);
            if (tanker != null)
            {
                tanker.CurrentBalance = running;
            }

            _dbcontext.SaveChanges();
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

                // 🔥 Safety net: agar kabhi Creation se pehle bhi koi backdated row ho, chain sahi rahegi
                RecalculateTankerLedger(request.Id);
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

            decimal oldBalance = tanker.CurrentBalance;

            tanker.TankerNo = updateTanker.TankerNo;
            tanker.OwnerId = updateTanker.OwnerId;
            tanker.Model = updateTanker.Model;
            tanker.Capacity = updateTanker.Capacity;
            tanker.CurrentBalance = updateTanker.CurrentBalance;

            decimal newBalance = updateTanker.CurrentBalance;

            _dbcontext.SaveChanges();

            decimal difference = newBalance - oldBalance;

            if (difference != 0)
            {
                decimal creditAmount = difference > 0 ? difference : 0;
                decimal debitAmount = difference < 0 ? Math.Abs(difference) : 0;

                string description = difference > 0
                    ? $"Balance manually increased via Tanker Edit. Tanker No: {tanker.TankerNo}"
                    : $"Balance manually decreased via Tanker Edit. Tanker No: {tanker.TankerNo}";

                // Manual edit hamesha "abhi" ke waqt ka correction hota hai, isliye TransactionDate = pakTime theek hai
                TankerLedger ledgerLog = new TankerLedger()
                {
                    TankerId = tanker.Id,
                    TransactionDate = pakTime,
                    ModuleName = "Tanker Edit",
                    ReferenceId = tanker.Id,
                    Credit = creditAmount,
                    Debit = debitAmount,
                    RunningBalance = 0, // temp — recalculation se set hoga
                    Description = description,
                    CreatedAt = pakTime,
                    CreatedBy = tanker.UpdatedBy
                };

                _dbcontext.TankerLedgers.Add(ledgerLog);
                _dbcontext.SaveChanges();

                // 🔥 FIX: Manual edit ke baad bhi pura chain recalc, taake koi mismatch na reh jaye
                RecalculateTankerLedger(tanker.Id);
            }

            TempData["edit_tanker_message"] = "Tanker updated successfully.";
            return RedirectToAction("Index");
        }

        public IActionResult TankerBalanceHistoory(int id)
        {
            var tanker = _dbcontext.Tankers.FirstOrDefault(x => x.Id == id);
            if (tanker == null) return NotFound();

            ViewBag.Owners = _dbcontext.TankerOwners.ToList();

            ViewBag.LedgerHistory = _dbcontext.TankerLedgers
                .Where(x => x.TankerId == id)
                .OrderBy(x => x.TransactionDate)
                .ThenBy(x => x.Id)
                .ToList();

            return View(tanker);
        }
    }
}