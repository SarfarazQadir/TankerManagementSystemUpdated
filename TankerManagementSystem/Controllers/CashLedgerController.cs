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

        // ==========================================
        // 🔥 SHARED HELPER: Recalculate RunningBalance
        // ==========================================
        private void RecalculateTankerLedger(int tankerId)
        {
            var rows = _db.TankerLedgers
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

            var tanker = _db.Tankers.FirstOrDefault(t => t.Id == tankerId);
            if (tanker != null)
            {
                tanker.CurrentBalance = running;
            }

            _db.SaveChanges();
        }

        public IActionResult Index()
        {
            var data = (
                from c in _db.CashLedgers
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

        // GET: CashLedger/AddEntry
        public IActionResult AddEntry()
        {
            ViewBag.Tankers = _db.Tankers.OrderBy(t => t.TankerNo).ToList();
            ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
            return View();
        }

        // POST: CashLedger/AddEntry
        [HttpPost]
        public IActionResult AddEntry([Bind("EntryDate,TankerId,KhataPersonId,Description,Credit,Debit")] CashLedger model)
        {
            try
            {
                model.CreatedAt = DateTime.Now;
                model.PersonId = 1;

                ModelState.Remove("Tanker");
                ModelState.Remove("KhataPerson");

                decimal lastCashBalance = _db.CashLedgers
                                             .OrderByDescending(x => x.Id)
                                             .Select(x => x.Balance)
                                             .FirstOrDefault();

                model.Balance = lastCashBalance + (model.Credit - model.Debit);
                model.CreatedBy = User.Identity?.Name ?? "System Admin";

                _db.CashLedgers.Add(model);
                _db.SaveChanges();

                // 2. Double-Entry Logic & Tanker Balance Sync
                if (model.TankerId.HasValue)
                {
                    decimal currentTransactionNet = model.Credit - model.Debit;

                    var tankerLedgerEntry = new TankerLedger
                    {
                        TankerId = model.TankerId.Value,
                        TransactionDate = model.EntryDate, // ✅ Ye pehle se hi sahi tha
                        ModuleName = "Cash Ledger",
                        ReferenceId = model.Id,
                        Description = model.Description,
                        CreatedAt = DateTime.Now,
                        CreatedBy = model.CreatedBy,
                        Credit = model.Credit,
                        Debit = model.Debit,
                        RunningBalance = 0 // temp — RecalculateTankerLedger() se set hoga
                    };

                    _db.TankerLedgers.Add(tankerLedgerEntry);
                    _db.SaveChanges();

                    // 🔥 FIX: Bug #2 — ab "last Id" ke bajaye pura chain date-order mein recalc hota hai,
                    // isliye backdated entries bhi sahi jagah par sahi RunningBalance ke saath fit hoti hain
                    RecalculateTankerLedger(model.TankerId.Value);
                }

                // 3. Khata Person Sync (ye already sahi tha, isme koi Tanker-related bug nahi tha)
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
                        CreatedAt = DateTime.Now,
                        CreatedBy = model.CreatedBy,
                        ModuleName = "Cash Ledger",
                        ReferenceId = model.Id
                    };
                    _db.PersonalKhatas.Add(khataEntry);

                    var khataPerson = _db.KhataPersons.FirstOrDefault(k => k.Id == model.KhataPersonId.Value);
                    if (khataPerson != null)
                    {
                        khataPerson.CurrentBalance += addAmt - minusAmt;
                        khataPerson.UpdatedAt = DateTime.Now;
                        khataPerson.UpdatedBy = model.CreatedBy;
                        _db.KhataPersons.Update(khataPerson);
                    }

                    _db.SaveChanges();
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
        public IActionResult EditEntry([Bind("Id,EntryDate,TankerId,KhataPersonId,Description,Credit,Debit,PersonId")] CashLedger model)
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

                var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                model.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
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

                decimal previousBalance = 0;
                var previousEntry = _db.CashLedgers
                    .Where(x => x.Id < model.Id)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefault();

                if (previousEntry != null)
                {
                    previousBalance = previousEntry.Balance;
                }

                decimal newBalance = previousBalance + model.Credit - model.Debit;

                if (newBalance < 0)
                {
                    TempData["error"] = $"Insufficient Balance! Current Balance is {previousBalance}";
                    ViewBag.Tankers = _db.Tankers.OrderBy(t => t.TankerNo).ToList();
                    ViewBag.Khatas = _db.KhataPersons.OrderBy(k => k.Name).ToList();
                    return View(model);
                }

                model.Balance = newBalance;
                model.CreatedAt = old.CreatedAt;
                model.CreatedBy = old.CreatedBy;

                _db.CashLedgers.Update(model);
                _db.SaveChanges();

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
                        // RunningBalance yahan set nahi karna — recalculation karega
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
                            CreatedAt = DateTime.Now,
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

                // 🔥 FIX: Bug #4 — Tanker badal bhi jaye ya same rahe, dono affected tankers ki
                // chain ko poori tarah recalc karo (agar old aur new tanker alag hain to dono)
                if (affectedOldTankerId.HasValue)
                {
                    RecalculateTankerLedger(affectedOldTankerId.Value);
                }
                if (affectedNewTankerId.HasValue && affectedNewTankerId != affectedOldTankerId)
                {
                    RecalculateTankerLedger(affectedNewTankerId.Value);
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
                    _db.PersonalKhatas.Remove(oldKhataEntry);
                    _db.SaveChanges();
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
                        CreatedAt = DateTime.Now,
                        CreatedBy = old.CreatedBy,
                        ModuleName = "Cash Ledger",
                        ReferenceId = model.Id
                    };
                    _db.PersonalKhatas.Add(newKhataEntry);

                    var person = _db.KhataPersons.FirstOrDefault(k => k.Id == model.KhataPersonId.Value);
                    if (person != null)
                    {
                        person.CurrentBalance += addAmt - minusAmt;
                        person.UpdatedAt = DateTime.Now;
                        person.UpdatedBy = currentUserId;
                        _db.KhataPersons.Update(person);
                    }

                    _db.SaveChanges();
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

            if (data.KhataPersonId.HasValue)
            {
                var khataEntry = _db.PersonalKhatas
                    .FirstOrDefault(x => x.ModuleName == "Cash Ledger" && x.ReferenceId == data.Id);
                if (khataEntry != null)
                {
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

            // 🔥 FIX: Delete ke baad bhi is tanker ki poori chain recalc karo
            // (pehle Tanker.CurrentBalance turant -= ho jata tha, baaki rows stale reh jati thi)
            if (affectedTankerId.HasValue)
            {
                RecalculateTankerLedger(affectedTankerId.Value);
            }

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

            var data = query.OrderByDescending(x => x.EntryDate)
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
            int currentMonth = month ?? DateTime.Now.Month;
            int currentYear = year ?? DateTime.Now.Year;

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