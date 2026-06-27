using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.ViewModels;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AuthorizeModule]
    public class CashLedgerController : Controller
    {
        private readonly ApplicationDbContext _db;

        public CashLedgerController(ApplicationDbContext db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
            var data = (
                from c in _db.CashLedgers

                    /*    join a in _db.tbl_admin
                        on c.CreatedBy equals a.admin_id*/

                orderby c.Id descending

                select new CashLedgerVM
                {
                    Id = c.Id,
                    EntryDate = c.EntryDate,
                    Description = c.Description,
                    Credit = c.Credit,
                    Debit = c.Debit,
                    Balance = c.Balance,
                    CreatedByName = c.CreatedBy
                }

            ).ToList();

            return View(data);
        }

        // =========================
        // ADD PAGE
        // =========================
        public IActionResult AddEntry()
        {
            return View();
        }

        // =========================
        // ADD POST
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddEntry(CashLedger model)
        {
            try
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

                // Pakistan Time
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");

                model.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    tz);

                model.CreatedBy = currentUserId;

                // Validation
                if (model.Credit > 0 && model.Debit > 0)
                {
                    TempData["error"] = "Only Credit OR Debit allowed.";

                    return View(model);
                }

                if (model.Credit <= 0 && model.Debit <= 0)
                {
                    TempData["error"] = "Enter Credit or Debit.";

                    return View(model);
                }

                // Previous Balance
                decimal previousBalance = _db.CashLedgers
                    .OrderByDescending(x => x.Id)
                    .Select(x => x.Balance)
                    .FirstOrDefault();

                // New Balance
                decimal newBalance = previousBalance + model.Credit - model.Debit;

                // Prevent Negative Balance
                if (newBalance < 0)
                {
                    TempData["error"] =
                        $"Insufficient Balance! Current Balance is {previousBalance}";

                    return View(model);
                }

                // Save Balance
                model.Balance = newBalance;

                _db.CashLedgers.Add(model);

                _db.SaveChanges();

                TempData["success"] = "Cash Ledger Added Successfully";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;

                return View(model);
            }
        }

        // =========================
        // EDIT PAGE
        // =========================
        public IActionResult EditEntry(int id)
        {
            var data = _db.CashLedgers.Find(id);

            if (data == null)
            {
                return NotFound();
            }

            return View(data);
        }

        // =========================
        // EDIT POST
        // =========================
        /*   [HttpPost]
           [ValidateAntiForgeryToken]
           public IActionResult EditEntry(CashLedger model)
           {
               try
               {
                   var old = _db.CashLedgers
                       .AsNoTracking()
                       .FirstOrDefault(x => x.Id == model.Id);

                   if (old == null)
                   {
                       return NotFound();
                   }

                   var session = HttpContext.Session.GetString("admin_session");

                   int userId = int.Parse(session);

                   var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");

                   model.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(
                       DateTime.UtcNow,
                       tz);

                   model.UpdatedBy = userId;

                   // Validation
                   if (model.Credit > 0 && model.Debit > 0)
                   {
                       TempData["error"] = "Only Credit OR Debit allowed.";

                       return View(model);
                   }

                   decimal previousBalance = 0;

                   var previousEntry = _db.CashLedgers
                       .Where(x => x.Id < model.Id)
                       .OrderByDescending(x => x.Id)
                       .FirstOrDefault();

                   if (previousEntry != null)
                   {
                       previousBalance = previousEntry.Balance;
                   }

                   model.Balance =
                       previousBalance
                       + model.Credit
                       - model.Debit;

                   model.CreatedAt = old.CreatedAt;
                   model.CreatedBy = old.CreatedBy;

                   _db.CashLedgers.Update(model);

                   _db.SaveChanges();

                   TempData["success"] = "Cash Ledger Updated Successfully";

                   return RedirectToAction("Index");
               }
               catch (Exception ex)
               {
                   TempData["error"] = ex.Message;

                   return View(model);
               }
           }*/

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditEntry(CashLedger model)
        {
            try
            {
                var old = _db.CashLedgers.AsNoTracking()
                    .FirstOrDefault(x => x.Id == model.Id);

                if (old == null)
                {
                    return NotFound();
                }

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

                var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");

                model.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    tz);

                model.UpdatedBy = currentUserId;

                // Validation
                if (model.Credit > 0 && model.Debit > 0)
                {
                    TempData["error"] = "Only Credit OR Debit allowed.";

                    return View(model);
                }

                if (model.Credit <= 0 && model.Debit <= 0)
                {
                    TempData["error"] = "Enter Credit or Debit.";

                    return View(model);
                }

                // Previous Balance
                decimal previousBalance = 0;

                var previousEntry = _db.CashLedgers
                    .Where(x => x.Id < model.Id)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefault();

                if (previousEntry != null)
                {
                    previousBalance = previousEntry.Balance;
                }

                // New Balance
                decimal newBalance =
                    previousBalance
                    + model.Credit
                    - model.Debit;

                // Prevent Negative Balance
                if (newBalance < 0)
                {
                    TempData["error"] =
                        $"Insufficient Balance! Current Balance is {previousBalance}";

                    return View(model);
                }

                // Save Balance
                model.Balance = newBalance;

                model.CreatedAt = old.CreatedAt;
                model.CreatedBy = old.CreatedBy;

                _db.CashLedgers.Update(model);

                _db.SaveChanges();

                TempData["success"] = "Cash Ledger Updated Successfully";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;

                return View(model);
            }
        }

        // =========================
        // DELETE
        // =========================
        public IActionResult DeleteEntry(int id)
        {
            var data = _db.CashLedgers.Find(id);

            if (data == null)
            {
                return NotFound();
            }

            _db.CashLedgers.Remove(data);

            _db.SaveChanges();

            TempData["success"] = "Cash Ledger Deleted Successfully";

            return RedirectToAction("Index");
        }



        // =====================================
        // DAILY CASH REPORT
        // =====================================
        public IActionResult DailyReport(DateTime? date)
        {
            DateTime reportDate = date ?? DateTime.Today;

            var data = _db.CashLedgers
                .Where(x => x.EntryDate.Date == reportDate.Date)
                .OrderBy(x => x.EntryDate)
                .ToList();

            ViewBag.Date = reportDate;

            return View(data);
        }
        // =====================================
        // ALL CASH REPORT (Gets all records)
        // =====================================
        public IActionResult AllReport()
        {
            // Database se saara data uthayein aur date ke hisab se sort karein
            var data = _db.CashLedgers
                .OrderByDescending(x => x.EntryDate)
                .ToList();

            // Report print karne ki tareekh ke liye
            ViewBag.PrintDate = DateTime.Now;

            return View(data);
        }
        // =====================================
        // MONTHLY CASH REPORT
        // =====================================
        public IActionResult MonthlyReport(int? month, int? year)
        {
            int currentMonth = month ?? DateTime.Now.Month;
            int currentYear = year ?? DateTime.Now.Year;

            var data = _db.CashLedgers
                .Where(x =>
                    x.EntryDate.Month == currentMonth
                    &&
                    x.EntryDate.Year == currentYear)
                .OrderBy(x => x.EntryDate)
                .ToList();

            ViewBag.Month = currentMonth;
            ViewBag.Year = currentYear;

            return View(data);
        }

        // =====================================
        // PROFIT LOSS REPORT
        // =====================================
        public IActionResult ProfitLoss(int? month, int? year)
        {
            int currentMonth = month ?? DateTime.Now.Month;
            int currentYear = year ?? DateTime.Now.Year;

            var data = _db.CashLedgers
                .Where(x =>
                    x.EntryDate.Month == currentMonth
                    &&
                    x.EntryDate.Year == currentYear)
                .ToList();

            decimal totalCredit = data.Sum(x => x.Credit);
            decimal totalDebit = data.Sum(x => x.Debit);

            ViewBag.TotalCredit = totalCredit;
            ViewBag.TotalDebit = totalDebit;
            ViewBag.ProfitLoss = totalCredit - totalDebit;
            ViewBag.Month = currentMonth;
            ViewBag.Year = currentYear;

            return View(data);
        }

        // =====================================
        // EXPENSE ANALYSIS
        // =====================================

        public class ExpenseAnalysisViewModel
        {
            public string Description { get; set; }
            public decimal TotalAmount { get; set; }
        }
        public IActionResult ExpenseAnalysis()
        {
            var data = _db.CashLedgers
                .Where(x => x.Debit > 0)
                .GroupBy(x => x.Description)
                .Select(g => new ExpenseAnalysisViewModel
                {
                    Description = g.Key,
                    TotalAmount = g.Sum(x => x.Debit)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();

            return View(data);
        }

    }
}
