// Modified by AI
// Date: 2026-07-21
// Reason: H-05 — Replaced 4 duplicated private RecalculateTankerLedger and 1 duplicated
//         private RecalculateCashLedger methods with injected ILedgerRecalculationService.
//         H-07 — Added validation in AddEntry POST to ensure at least one of Credit
//         or Debit is provided and positive (was missing from Add, only existed on Edit).

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Helpers;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.ViewModels;
using TankerManagementSystem.Services;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AuthorizeModule]
    public class CashLedgerController : Controller
    {
        private readonly ApplicationDbContext _db;

        // Modified by AI
        // Date: 2026-07-21
        // Reason: H-05 — Injecting ILedgerRecalculationService replaces the private
        // RecalculateTankerLedger() and RecalculateCashLedger() methods that were
        // copy-pasted into this controller and 3 others.
        private readonly ILedgerRecalculationService _recalcService;

        public CashLedgerController(ApplicationDbContext db, ILedgerRecalculationService recalcService)
        {
            _db = db;
            _recalcService = recalcService;
        }

        public IActionResult Index()
        {
            var data = (
                from c in _db.CashLedgers
                orderby c.CreatedAt descending
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

        // GET: CashLedger/AddEntry
        public IActionResult AddEntry()
        {
            ViewBag.Tankers = _db.Tankers.OrderBy(t => t.TankerNo).ToList();
            ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
            return View();
        }

        // POST: CashLedger/AddEntry
        [HttpPost]
        public IActionResult AddEntry([Bind("EntryDate,TankerId,KhataPersonId,Description,Credit,Debit,IsGeneralExpense")] CashLedger model)
        {
            try
            {
                var pakTime = DateTimeHelper.GetPakistanTime();

                model.CreatedAt = pakTime;
                model.PersonId = 1;

                ModelState.Remove("Tanker");
                ModelState.Remove("KhataPerson");

                // Modified by AI
                // Date: 2026-07-21
                // Reason: H-07 — AddEntry was missing the credit/debit validation that
                // EditEntry already had. Without this, a zero-credit/zero-debit entry
                // could be saved, creating a phantom row that corrupts balance reporting.
                if (model.Credit > 0 && model.Debit > 0)
                {
                    TempData["error"] = "Only Credit OR Debit allowed, not both at the same time.";
                    ViewBag.Tankers = _db.Tankers.OrderBy(t => t.TankerNo).ToList();
                    ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
                    return View(model);
                }

                if (model.Credit <= 0 && model.Debit <= 0)
                {
                    TempData["error"] = "Enter either a Credit amount or a Debit amount.";
                    ViewBag.Tankers = _db.Tankers.OrderBy(t => t.TankerNo).ToList();
                    ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
                    return View(model);
                }

                // Modified by AI
                // Date: 2026-07-21
                // Reason: Calculate total available cash balance across the entire CashLedger (Sum of Credit - Debit).
                // Avoids retrieving a backdated row's intermediate running balance when backdated entries exist.
                decimal lastCashBalance = _db.CashLedgers.Sum(x => (decimal?)x.Credit - (decimal?)x.Debit) ?? 0;

                model.Balance = lastCashBalance + (model.Credit - model.Debit);
                model.CreatedBy = User.Identity?.Name ?? "System Admin";

                _db.CashLedgers.Add(model);
                _db.SaveChanges();

                // FIX Issue 03: Recalculate full cash ledger chain after adding
                _recalcService.RecalculateCashLedger();

                // 2. Double-Entry Logic & Tanker Balance Sync
                if (model.TankerId.HasValue)
                {
                    var tankerLedgerEntry = new TankerLedger
                    {
                        TankerId = model.TankerId.Value,
                        TransactionDate = model.EntryDate,
                        ModuleName = "Cash Ledger",
                        ReferenceId = model.Id,
                        Description = model.Description,
                        CreatedAt = pakTime,
                        CreatedBy = model.CreatedBy,
                        Credit = model.Credit,
                        Debit = model.Debit,
                        RunningBalance = 0 // temp — RecalculateTankerLedger() will set this
                    };

                    _db.TankerLedgers.Add(tankerLedgerEntry);
                    _db.SaveChanges();

                    _recalcService.RecalculateTankerLedger(model.TankerId.Value);
                }

                // 3. Khata Person Sync
                if (model.KhataPersonId.HasValue)
                {
                    decimal lastPersonBalance = _db.PersonalKhatas
                        .Where(x => x.KhataPersonId == model.KhataPersonId.Value)
                        .OrderByDescending(x => x.Id)
                        .Select(x => x.Balance)
                        .FirstOrDefault();

                    decimal addAmt = model.Credit;
                    decimal minusAmt = model.Debit;

                    var khataEntry = new PersonalKhata
                    {
                        EntryDate = model.EntryDate,
                        KhataPersonId = model.KhataPersonId.Value,
                        Description = model.Description,
                        AddAmount = addAmt,
                        MinusAmount = minusAmt,
                        Balance = lastPersonBalance + addAmt - minusAmt,
                        CreatedAt = pakTime,
                        CreatedBy = model.CreatedBy,
                        ModuleName = "Cash Ledger",
                        ReferenceId = model.Id
                    };
                    _db.PersonalKhatas.Add(khataEntry);

                    var khataPerson = _db.KhataPersons.FirstOrDefault(k => k.Id == model.KhataPersonId.Value);
                    if (khataPerson != null)
                    {
                        khataPerson.CurrentBalance += addAmt - minusAmt;
                        khataPerson.UpdatedAt = pakTime;
                        khataPerson.UpdatedBy = model.CreatedBy;
                        _db.KhataPersons.Update(khataPerson);
                    }

                    _db.SaveChanges();

                    // Recalculate PersonalKhata to ensure date-ordered chain is correct
                    _recalcService.RecalculatePersonalKhata(model.KhataPersonId.Value);
                }

                TempData["success"] = "Ledger entry updated and balances synchronized successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["error"] = "Process error: " + ex.Message;
                ViewBag.Tankers = _db.Tankers.OrderBy(t => t.TankerNo).ToList();
                ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
                return View(model);
            }
        }

        // GET: CashLedger/EditEntry
        public IActionResult EditEntry(int id)
        {
            var data = _db.CashLedgers.Find(id);
            if (data == null)
            {
                return NotFound();
            }

            ViewBag.Tankers = _db.Tankers.OrderBy(t => t.TankerNo).ToList();
            ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
            return View(data);
        }

        [HttpPost]
        public IActionResult EditEntry([Bind("Id,EntryDate,TankerId,KhataPersonId,Description,Credit,Debit,PersonId,IsGeneralExpense")] CashLedger model)
        {
            try
            {
                var old = _db.CashLedgers.AsNoTracking().FirstOrDefault(x => x.Id == model.Id);
                if (old == null)
                {
                    return NotFound();
                }

                ModelState.Remove("Tanker");
                ModelState.Remove("KhataPerson");

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

                var pakTime = DateTimeHelper.GetPakistanTime();
                model.UpdatedAt = pakTime;
                model.UpdatedBy = currentUserId;

                if (model.Credit > 0 && model.Debit > 0)
                {
                    TempData["error"] = "Only Credit OR Debit allowed.";
                    ViewBag.Tankers = _db.Tankers.OrderBy(t => t.TankerNo).ToList();
                    ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
                    return View(model);
                }

                if (model.Credit <= 0 && model.Debit <= 0)
                {
                    TempData["error"] = "Enter Credit or Debit.";
                    ViewBag.Tankers = _db.Tankers.OrderBy(t => t.TankerNo).ToList();
                    ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
                    return View(model);
                }

                // Set balance temporarily — RecalculateCashLedger will fix it
                model.Balance = 0;
                model.CreatedAt = old.CreatedAt;
                model.CreatedBy = old.CreatedBy;

                _db.CashLedgers.Update(model);
                _db.SaveChanges();

                _recalcService.RecalculateCashLedger();

                // ==========================================
                // TANKER MASTER TABLE + LEDGER SYNC (Edit)
                // ==========================================
                var oldTankerLedger = _db.TankerLedgers
                    .FirstOrDefault(x => x.ModuleName == "Cash Ledger" && x.ReferenceId == model.Id);

                int? affectedOldTankerId = old.TankerId;
                int? affectedNewTankerId = model.TankerId;

                if (model.TankerId.HasValue)
                {
                    if (oldTankerLedger != null)
                    {
                        oldTankerLedger.TankerId = model.TankerId.Value;
                        oldTankerLedger.TransactionDate = model.EntryDate;
                        oldTankerLedger.Description = model.Description;
                        oldTankerLedger.Credit = model.Credit;
                        oldTankerLedger.Debit = model.Debit;
                        oldTankerLedger.CreatedBy = old.CreatedBy;
                        _db.TankerLedgers.Update(oldTankerLedger);
                    }
                    else
                    {
                        var newTankerLedger = new TankerLedger
                        {
                            TankerId = model.TankerId.Value,
                            TransactionDate = model.EntryDate,
                            ModuleName = "Cash Ledger",
                            ReferenceId = model.Id,
                            Description = model.Description,
                            CreatedAt = pakTime,
                            CreatedBy = old.CreatedBy,
                            Credit = model.Credit,
                            Debit = model.Debit,
                            RunningBalance = 0
                        };
                        _db.TankerLedgers.Add(newTankerLedger);
                    }
                }
                else
                {
                    if (oldTankerLedger != null)
                    {
                        _db.TankerLedgers.Remove(oldTankerLedger);
                    }
                }

                _db.SaveChanges();

                // Recalculate both affected tanker chains
                if (affectedOldTankerId.HasValue)
                {
                    _recalcService.RecalculateTankerLedger(affectedOldTankerId.Value);
                }
                if (affectedNewTankerId.HasValue && affectedNewTankerId != affectedOldTankerId)
                {
                    _recalcService.RecalculateTankerLedger(affectedNewTankerId.Value);
                }

                // ==========================================
                // KHATA PERSON SYNC (Edit) — reverse old, apply new
                // ==========================================
                var oldKhataEntry = _db.PersonalKhatas
                    .FirstOrDefault(x => x.ModuleName == "Cash Ledger" && x.ReferenceId == model.Id);

                if (oldKhataEntry != null)
                {
                    var oldPerson = _db.KhataPersons.FirstOrDefault(k => k.Id == oldKhataEntry.KhataPersonId);
                    if (oldPerson != null)
                    {
                        oldPerson.CurrentBalance -= (oldKhataEntry.AddAmount - oldKhataEntry.MinusAmount);
                        _db.KhataPersons.Update(oldPerson);
                    }
                    int oldKhataPersonId = oldKhataEntry.KhataPersonId;
                    _db.PersonalKhatas.Remove(oldKhataEntry);
                    _db.SaveChanges();
                    _recalcService.RecalculatePersonalKhata(oldKhataPersonId);
                }

                if (model.KhataPersonId.HasValue)
                {
                    decimal lastPersonBalance = _db.PersonalKhatas
                        .Where(x => x.KhataPersonId == model.KhataPersonId.Value)
                        .OrderByDescending(x => x.Id)
                        .Select(x => x.Balance)
                        .FirstOrDefault();

                    decimal addAmt = model.Credit;
                    decimal minusAmt = model.Debit;

                    var newKhataEntry = new PersonalKhata
                    {
                        EntryDate = model.EntryDate,
                        KhataPersonId = model.KhataPersonId.Value,
                        Description = model.Description,
                        AddAmount = addAmt,
                        MinusAmount = minusAmt,
                        Balance = lastPersonBalance + addAmt - minusAmt,
                        CreatedAt = pakTime,
                        CreatedBy = old.CreatedBy,
                        ModuleName = "Cash Ledger",
                        ReferenceId = model.Id
                    };
                    _db.PersonalKhatas.Add(newKhataEntry);

                    var person = _db.KhataPersons.FirstOrDefault(k => k.Id == model.KhataPersonId.Value);
                    if (person != null)
                    {
                        person.CurrentBalance += addAmt - minusAmt;
                        person.UpdatedAt = pakTime;
                        person.UpdatedBy = currentUserId;
                        _db.KhataPersons.Update(person);
                    }

                    _db.SaveChanges();
                    _recalcService.RecalculatePersonalKhata(model.KhataPersonId.Value);
                }

                TempData["success"] = "Cash Ledger, Tanker and Khata balances updated successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;
                ViewBag.Tankers = _db.Tankers.OrderBy(t => t.TankerNo).ToList();
                ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
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

            int? affectedTankerId = data.TankerId;

            if (data.TankerId.HasValue)
            {
                var tankerLedger = _db.TankerLedgers
                    .FirstOrDefault(x => x.ModuleName == "Cash Ledger" && x.ReferenceId == data.Id);
                if (tankerLedger != null)
                {
                    _db.TankerLedgers.Remove(tankerLedger);
                }
            }

            int? affectedKhataPersonId = null;
            if (data.KhataPersonId.HasValue)
            {
                var khataEntry = _db.PersonalKhatas
                    .FirstOrDefault(x => x.ModuleName == "Cash Ledger" && x.ReferenceId == data.Id);
                if (khataEntry != null)
                {
                    affectedKhataPersonId = khataEntry.KhataPersonId;
                    var person = _db.KhataPersons.FirstOrDefault(k => k.Id == khataEntry.KhataPersonId);
                    if (person != null)
                    {
                        person.CurrentBalance -= (khataEntry.AddAmount - khataEntry.MinusAmount);
                        _db.KhataPersons.Update(person);
                    }
                    _db.PersonalKhatas.Remove(khataEntry);
                }
            }

            _db.CashLedgers.Remove(data);
            _db.SaveChanges();

            _recalcService.RecalculateCashLedger();

            if (affectedTankerId.HasValue)
            {
                _recalcService.RecalculateTankerLedger(affectedTankerId.Value);
            }

            if (affectedKhataPersonId.HasValue)
            {
                _recalcService.RecalculatePersonalKhata(affectedKhataPersonId.Value);
            }

            TempData["success"] = "Cash Ledger Deleted Successfully";
            return RedirectToAction("Index");
        }

        // =====================================
        // DAILY CASH REPORT
        // =====================================
        public IActionResult DailyReport(DateTime? date)
        {
            DateTime reportDate = date ?? DateTimeHelper.GetPakistanToday();

            var data = _db.CashLedgers
                .Where(x => x.EntryDate.Date == reportDate.Date)
                .OrderBy(x => x.EntryDate)
                .ToList();

            ViewBag.Date = reportDate;
            return View(data);
        }

        // =====================================
        // ALL CASH REPORT
        // =====================================
        public IActionResult AllReport(DateTime? startDate, DateTime? endDate)
        {
            var query = _db.CashLedgers.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(x => x.EntryDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(x => x.EntryDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));
            }

            var data = query.OrderByDescending(x => x.CreatedAt)
                            .Select(x => new CashLedgerVM
                            {
                                Id = x.Id,
                                EntryDate = x.EntryDate,
                                Description = x.Description,
                                Credit = x.Credit,
                                Debit = x.Debit,
                                Balance = x.Balance,
                                CreatedByName = x.CreatedBy
                            })
                            .ToList();

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.PrintDate = DateTimeHelper.GetPakistanTime();

            return View(data);
        }

        // =====================================
        // MONTHLY CASH REPORT
        // =====================================
        public IActionResult MonthlyReport(int? month, int? year)
        {
            var pakTime = DateTimeHelper.GetPakistanTime();
            int currentMonth = month ?? pakTime.Month;
            int currentYear = year ?? pakTime.Year;

            var data = _db.CashLedgers
                .Where(x => x.EntryDate.Month == currentMonth && x.EntryDate.Year == currentYear)
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
            var pakTime = DateTimeHelper.GetPakistanTime();
            int currentMonth = month ?? pakTime.Month;
            int currentYear = year ?? pakTime.Year;

            var data = _db.CashLedgers
                .Where(x => x.EntryDate.Month == currentMonth && x.EntryDate.Year == currentYear)
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