// Modified by AI
// Date: 2026-07-21
// Reason: C-01 — Added _recalcService.RecalculateCashLedger() call at the end of
//         AddEntry POST. This was the only write path that never called RecalculateCashLedger(),
//         causing the CashLedger balance chain to remain incorrect after adding a trip.
//         C-02 — Added _recalcService.RecalculateCashLedger() call at the end of
//         EditEntry POST. Same issue as C-01 applied to the edit flow.
//         H-05 — Injected ILedgerRecalculationService replacing previous inability
//         to call RecalculateCashLedger() (which was a private method in CashLedgerController).

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Models;
using TankerManagementSystem.Services;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AuthorizeModule]
    public class TripEntryController : Controller
    {
        private readonly ApplicationDbContext _db;

        // Modified by AI
        // Date: 2026-07-21
        // Reason: H-05 / C-01 / C-02 — Service injection enables RecalculateCashLedger()
        // to be called from this controller (previously impossible since that logic was
        // private inside CashLedgerController).
        private readonly ILedgerRecalculationService _recalcService;

        public TripEntryController(ApplicationDbContext db, ILedgerRecalculationService recalcService)
        {
            _db = db;
            _recalcService = recalcService;
        }

        // LIST
        public IActionResult Index()
        {
            var trips = _db.TripEntries
                .Include(x => x.TankerFk)
                .OrderByDescending(x => x.Id)
                .ToList();

            return View(trips);
        }

        // ADD GET
        public IActionResult AddEntry()
        {
            ViewBag.Tankers = _db.Tankers.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddEntry(TripEntry request, List<TripExpense> expenses)
        {
            using var transaction = _db.Database.BeginTransaction();
            try
            {
                var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);
                request.CreatedAt = pakTime;

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
                request.CreatedBy = currentUserId;

                if (request.TankerId == 0 || string.IsNullOrWhiteSpace(request.ToLocation))
                {
                    TempData["Error"] = "Required fields missing";
                    return RedirectToAction("AddEntry");
                }

                // FIX Issue 07: Compare LoadDate (operational date) instead of CreatedAt
                bool alreadyExists = _db.TripEntries.Any(x =>
                    x.TankerId == request.TankerId &&
                    x.FromLocation == request.FromLocation &&
                    x.ToLocation == request.ToLocation &&
                    x.LoadDate.Date == request.LoadDate.Date
                );

                if (alreadyExists)
                {
                    TempData["Error"] = "Same tanker already has an entry for this route on the same date.";
                    TempData["toast_error"] = "Duplicate entry not allowed.";
                    return RedirectToAction("AddEntry");
                }

                // Clean & Filter Dynamic Expenses
                if (expenses == null) expenses = new List<TripExpense>();
                var uniqueExpenses = expenses
                    .Where(x => !string.IsNullOrWhiteSpace(x.ExpenseName) && x.Amount > 0)
                    .GroupBy(x => new { ExpenseName = x.ExpenseName.Trim().ToLower(), x.Amount })
                    .Select(g => g.First())
                    .ToList();

                decimal totalExpense = uniqueExpenses.Sum(x => x.Amount);
                decimal totalCashRequired = request.AdvanceCash + totalExpense;

                // Cash Balance Validation
                // Modified by AI
                // Date: 2026-07-21
                // Reason: Calculate total available cash balance across the entire CashLedger (Sum of Credit - Debit).
                // Previously, OrderByDescending(x => x.Id).FirstOrDefault().Balance fetched a backdated row's
                // intermediate running balance instead of the overall cash balance, causing false
                // "Cash Ledger balance is low" toaster errors when creating historical/backdated trip entries.
                decimal currentCashBalance = _db.CashLedgers.Sum(x => (decimal?)x.Credit - (decimal?)x.Debit) ?? 0;

                if (currentCashBalance < totalCashRequired)
                {
                    TempData["Error"] = $"Cash Ledger balance is low. Required: {totalCashRequired}, Available: {currentCashBalance}";
                    return RedirectToAction("AddEntry");
                }

                // Save Trip Entry
                _db.TripEntries.Add(request);
                _db.SaveChanges(); // Generates request.Id

                // Save Trip Expenses linked to Trip Entry
                if (uniqueExpenses.Count > 0)
                {
                    foreach (var item in uniqueExpenses)
                    {
                        item.TripEntryId = request.Id;
                        item.CreatedAt = pakTime;
                        item.CreatedBy = currentUserId;
                        _db.TripExpenses.Add(item);
                    }
                }

                // --- CASH LEDGER LOGS (Deduction Happens Here Only) ---
                if (request.AdvanceCash > 0)
                {
                    currentCashBalance -= request.AdvanceCash;
                    _db.CashLedgers.Add(new CashLedger()
                    {
                        EntryDate = request.LoadDate,
                        Description = $"Trip Entry Advance Cash Payment | Tanker ID: {request.TankerId} | Trip Entry Ref: {request.Id}",
                        Debit = request.AdvanceCash,
                        Credit = 0,
                        Balance = currentCashBalance,
                        CreatedAt = pakTime,
                        CreatedBy = currentUserId
                    });
                }

                if (uniqueExpenses.Count > 0)
                {
                    foreach (var item in uniqueExpenses)
                    {
                        currentCashBalance -= item.Amount;
                        _db.CashLedgers.Add(new CashLedger()
                        {
                            EntryDate = request.LoadDate,
                            Description = $"Trip Entry Expense Log: {item.ExpenseName} | Trip Entry Ref: {request.Id}",
                            Debit = item.Amount,
                            Credit = 0,
                            Balance = currentCashBalance,
                            CreatedAt = pakTime,
                            CreatedBy = currentUserId
                        });
                    }
                }

                _db.SaveChanges();

                // Modified by AI
                // Date: 2026-07-21
                // Reason: C-01 — This call was completely missing from AddEntry. Without it,
                // the locally-computed currentCashBalance values written to CashLedger.Balance
                // could be stale/wrong if any prior entry had the wrong balance, or if a
                // concurrent request inserted a row between the read and the saves.
                // RecalculateCashLedger() ensures the entire chain is correctly ordered and balanced.
                _recalcService.RecalculateCashLedger();

                transaction.Commit();

                TempData["add_trip_message"] = "Trip and expenses added successfully, Cash Ledger updated.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Error"] = "An error occurred: " + ex.Message;
                return RedirectToAction("AddEntry");
            }
        }

        // EDIT GET
        public IActionResult EditEntry(int id)
        {
            var trip = _db.TripEntries
                .Include(x => x.TripExpenses)
                .FirstOrDefault(x => x.Id == id);

            if (trip == null) return NotFound();

            ViewBag.Tankers = _db.Tankers.ToList();
            return View(trip);
        }

        // EDIT POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditEntry(TripEntry update, List<TripExpense> expenses)
        {
            using var transaction = _db.Database.BeginTransaction();
            try
            {
                var trip = _db.TripEntries
                    .Include(x => x.TripExpenses)
                    .FirstOrDefault(x => x.Id == update.Id);

                if (trip == null) return NotFound();

                // 1. Session & Auth Check
                var currentUserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                    ?? User?.FindFirst(ClaimTypes.Name)?.Value
                                    ?? User?.FindFirst("sub")?.Value
                                    ?? User?.FindFirst(ClaimTypes.Email)?.Value
                                    ?? User?.Identity?.Name;

                if (string.IsNullOrEmpty(currentUserId) || !(User?.Identity?.IsAuthenticated ?? false))
                {
                    TempData["Error"] = "Session expired. Please login again.";
                    return RedirectToAction("Login", "Admin");
                }

                // 2. Duplicate Check
                bool alreadyExists = _db.TripEntries.Any(x =>
                    x.Id != update.Id &&
                    x.TankerId == update.TankerId &&
                    x.FromLocation == update.FromLocation &&
                    x.ToLocation == update.ToLocation &&
                    x.LoadDate.Date == update.LoadDate.Date
                );

                if (alreadyExists)
                {
                    TempData["Error"] = "Same tanker already has an entry for this route on the same date.";
                    return RedirectToAction("EditEntry", new { id = update.Id });
                }

                var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);

                // Modified by AI
                // Date: 2026-07-21
                // Reason: Calculate total available cash balance across the entire CashLedger (Sum of Credit - Debit).
                // Previously, OrderByDescending(x => x.Id).FirstOrDefault().Balance fetched a backdated row's
                // intermediate running balance instead of the overall cash balance, causing false
                // "Insufficient cash balance" toaster errors when editing historical/backdated trip entries.
                decimal currentCashBalance = _db.CashLedgers.Sum(x => (decimal?)x.Credit - (decimal?)x.Debit) ?? 0;

                // Purana Advance Cash wapas balance me add karein
                if (trip.AdvanceCash > 0)
                {
                    currentCashBalance += trip.AdvanceCash;
                    _db.CashLedgers.Add(new CashLedger()
                    {
                        EntryDate = update.LoadDate,
                        Description = $"[REVERSED FOR EDIT] Trip Entry Advance Cash | Trip Entry Ref: {trip.Id}",
                        Debit = 0,
                        Credit = trip.AdvanceCash, // Cash wapas aaya
                        Balance = currentCashBalance,
                        CreatedAt = pakTime,
                        CreatedBy = currentUserId
                    });
                }

                // Purane Expenses wapas balance me add karein
                decimal oldExpensesTotal = trip.TripExpenses.Sum(x => x.Amount);
                if (oldExpensesTotal > 0)
                {
                    currentCashBalance += oldExpensesTotal;
                    _db.CashLedgers.Add(new CashLedger()
                    {
                        EntryDate = update.LoadDate,
                        Description = $"[REVERSED FOR EDIT] Total Expenses Reversed | Trip Entry Ref: {trip.Id}",
                        Debit = 0,
                        Credit = oldExpensesTotal, // Cash wapas aaya
                        Balance = currentCashBalance,
                        CreatedAt = pakTime,
                        CreatedBy = currentUserId
                    });
                }

                // Clean & Filter New Incoming Expenses
                if (expenses == null) expenses = new List<TripExpense>();
                var uniqueNewExpenses = expenses
                    .Where(x => !string.IsNullOrWhiteSpace(x.ExpenseName) && x.Amount > 0)
                    .GroupBy(x => new { ExpenseName = x.ExpenseName.Trim().ToLower(), x.Amount })
                    .Select(g => g.First())
                    .ToList();

                decimal totalNewExpenses = uniqueNewExpenses.Sum(x => x.Amount);
                decimal netNewCashRequired = update.AdvanceCash + totalNewExpenses;

                // 3. Validation Check after reversal
                if (currentCashBalance < netNewCashRequired)
                {
                    TempData["Error"] = $"Insufficient cash balance for these updates. Required: {netNewCashRequired}, Available: {currentCashBalance}";
                    return RedirectToAction("EditEntry", new { id = update.Id });
                }

                // =========================================================================
                // STEP B: APPLY UPDATED VALUES & DEDUCTIONS
                // =========================================================================

                // Remove old expenses from database table
                if (trip.TripExpenses.Any())
                {
                    _db.TripExpenses.RemoveRange(trip.TripExpenses);
                }

                // Update core trip entry fields
                trip.LoadDate = update.LoadDate;
                trip.TankerId = update.TankerId;
                trip.AdvanceCash = update.AdvanceCash;
                trip.FromLocation = update.FromLocation;
                trip.ToLocation = update.ToLocation;
                trip.UpdatedAt = pakTime;
                trip.UpdatedBy = currentUserId;

                // Insert fresh updated expenses
                foreach (var item in uniqueNewExpenses)
                {
                    _db.TripExpenses.Add(new TripExpense()
                    {
                        TripEntryId = trip.Id,
                        ExpenseName = item.ExpenseName,
                        Amount = item.Amount,
                        CreatedAt = pakTime,
                        CreatedBy = currentUserId
                    });
                }

                // Charge new Advance Cash to Cash Ledger
                if (update.AdvanceCash > 0)
                {
                    currentCashBalance -= update.AdvanceCash;
                    _db.CashLedgers.Add(new CashLedger()
                    {
                        EntryDate = update.LoadDate,
                        Description = $"[UPDATED] Trip Entry Advance Cash Payment | Tanker ID: {update.TankerId} | Trip Entry Ref: {trip.Id}",
                        Debit = update.AdvanceCash,
                        Credit = 0,
                        Balance = currentCashBalance,
                        CreatedAt = pakTime,
                        CreatedBy = currentUserId
                    });
                }

                // Charge new Expenses to Cash Ledger
                foreach (var item in uniqueNewExpenses)
                {
                    currentCashBalance -= item.Amount;
                    _db.CashLedgers.Add(new CashLedger()
                    {
                        EntryDate = update.LoadDate,
                        Description = $"[UPDATED] Trip Expense Log: {item.ExpenseName} | Trip Entry Ref: {trip.Id}",
                        Debit = item.Amount,
                        Credit = 0,
                        Balance = currentCashBalance,
                        CreatedAt = pakTime,
                        CreatedBy = currentUserId
                    });
                }

                _db.SaveChanges();

                // Modified by AI
                // Date: 2026-07-21
                // Reason: C-02 — This call was completely missing from EditEntry POST.
                // After reversing and re-applying cash entries via locally-tracked
                // currentCashBalance, the entire CashLedger chain must be recalculated
                // to ensure correctness — especially for any entries that might have been
                // inserted out of date order (backdated edit).
                _recalcService.RecalculateCashLedger();

                transaction.Commit();

                TempData["edit_trip_message"] = "Trip and expenses updated successfully. Ledger adjusted.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Error"] = "Error updating trip: " + ex.Message;
                return RedirectToAction("EditEntry", new { id = update.Id });
            }
        }

    }
}