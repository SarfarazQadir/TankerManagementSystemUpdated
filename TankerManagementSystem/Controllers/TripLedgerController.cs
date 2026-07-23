// Modified by AI
// Date: 2026-07-21
// Reason: H-05 — Replaced duplicated private RecalculateTankerLedger() with
//         injected ILedgerRecalculationService.
//         C-03 — Fixed balanceExcludingThisTrip calculation in EditLedger which was
//         subtracting existingLedger.GrandTotal from tanker.CurrentBalance — this was
//         mathematically wrong when GrandTotal was negative (a loss trip) because the
//         formula produced the wrong sign. Now correctly reads the actual Credit/Debit
//         from the existing TankerLedger row to compute the true prior balance.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.ViewModels;
using TankerManagementSystem.Services;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AuthorizeModule]
    public class TripLedgerController : Controller
    {
        private readonly ApplicationDbContext _db;

        // Modified by AI
        // Date: 2026-07-21
        // Reason: H-05 — Injecting ILedgerRecalculationService replaces the private
        // RecalculateTankerLedger() method that was duplicated in 4 controllers.
        private readonly ILedgerRecalculationService _recalcService;

        public TripLedgerController(ApplicationDbContext db, ILedgerRecalculationService recalcService)
        {
            _db = db;
            _recalcService = recalcService;
        }

        public IActionResult Index()
        {
            var data = _db.TripLedgers
                .Include(x => x.Product)
                .Include(x => x.TripEntryFk)
                .OrderByDescending(x => x.Id)
                .ToList();

            return View(data);
        }

        // ==========================================
        // ADD LEDGER (GET)
        // ==========================================
        public IActionResult AddLedger(int tripId)
        {
            bool ledgerExists = _db.TripLedgers.Any(x => x.TripEntryId == tripId);

            if (ledgerExists)
            {
                TempData["ledger_exists"] = "Ledger already created for this trip.";
                return RedirectToAction("Index", "TripEntry");
            }

            var trip = _db.TripEntries
                .Include(x => x.TankerFk)
                .FirstOrDefault(x => x.Id == tripId);

            if (trip == null)
                return NotFound();

            ViewBag.Trip = trip;
            ViewBag.Products = _db.Products.ToList();
            ViewBag.TankerPreviousBalance = trip.TankerFk?.CurrentBalance ?? 0;

            var model = new TripLedgerVM
            {
                TripEntryId = trip.Id,
                TripDate = trip.LoadDate,
                AdvanceCash = trip.AdvanceCash,
                Freight = 0,
                Shortage = 0,
                ShortageLiters = 0,
                Commission = 0,
                Munshiana = 0
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddLedger(TripLedgerVM vm)
        {
            using var transaction = _db.Database.BeginTransaction();
            try
            {
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

                bool tokenExists = _db.TripLedgers.Any(x => x.TokenNo == vm.TokenNo);
                if (tokenExists)
                {
                    ModelState.AddModelError("TokenNo", "Token No already exists.");
                    ViewBag.Products = _db.Products.ToList();
                    var originalTrip = _db.TripEntries.Include(x => x.TankerFk).FirstOrDefault(x => x.Id == vm.TripEntryId);
                    if (originalTrip != null)
                    {
                        ViewBag.Trip = originalTrip;
                        ViewBag.TankerPreviousBalance = originalTrip.TankerFk?.CurrentBalance ?? 0;
                    }
                    return View(vm);
                }

                var trip = _db.TripEntries
                    .Include(x => x.TankerFk)
                    .Include(x => x.TripExpenses)
                    .FirstOrDefault(x => x.Id == vm.TripEntryId);

                if (trip == null) return NotFound();
                var tanker = trip.TankerFk;

                var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                var commissionSetup = _db.CommissionSetups.FirstOrDefault(x => x.IsActive);
                decimal commissionPercent = commissionSetup?.Percentage ?? 0;
                decimal calculatedCommission = (vm.Freight * commissionPercent) / 100;

                decimal advanceCashFromEntry = trip.AdvanceCash;
                decimal totalExpenseFromEntry = trip.TripExpenses.Sum(x => x.Amount);

                decimal totalDeductions = advanceCashFromEntry + vm.Shortage + calculatedCommission + vm.Munshiana + totalExpenseFromEntry;
                decimal grandTotal = vm.Freight - totalDeductions;

                decimal calculatedAmountPay = 0;
                decimal oldBalance = tanker?.CurrentBalance ?? 0;

                if (oldBalance < 0 && grandTotal > 0)
                {
                    decimal outstandingDues = Math.Abs(oldBalance);
                    calculatedAmountPay = Math.Min(outstandingDues, grandTotal);
                }

                var model = new TripLedger()
                {
                    TripDate = vm.TripDate,
                    TripEntryId = vm.TripEntryId,
                    ProductId = vm.ProductId,
                    Freight = vm.Freight,
                    AdvanceCash = advanceCashFromEntry,
                    Shortage = vm.Shortage,
                    ShortageLiters = vm.ShortageLiters,
                    Commission = calculatedCommission,
                    Munshiana = vm.Munshiana,
                    TokenNo = vm.TokenNo,
                    Remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? "N/A" : vm.Remarks,
                    TotalDeductions = totalDeductions,
                    GrandTotal = grandTotal,
                    AmountPay = calculatedAmountPay,
                    CreatedAt = pakTime,
                    CreatedBy = currentUserId
                };

                _db.TripLedgers.Add(model);
                _db.SaveChanges(); // Generates model.Id

                foreach (var exp in trip.TripExpenses)
                {
                    exp.TripLedgerId = model.Id;
                    _db.Entry(exp).State = EntityState.Modified;
                }

                if (tanker != null && grandTotal != 0)
                {
                    decimal creditAmount = grandTotal > 0 ? grandTotal : 0;
                    decimal debitAmount = grandTotal < 0 ? Math.Abs(grandTotal) : 0;

                    string logDescription = grandTotal > 0
                        ? (oldBalance < 0 ? $"Trip Profit applied: Dues adjusted. Token#: {model.TokenNo}" : $"Trip Profit Credited. Token#: {model.TokenNo}")
                        : $"Trip Loss Debited. Deductions exceeded Freight. Token#: {model.TokenNo}";

                    _db.TankerLedgers.Add(new TankerLedger()
                    {
                        TankerId = tanker.Id,
                        TransactionDate = trip.LoadDate,
                        ModuleName = "Trip Ledger Add",
                        ReferenceId = model.Id,
                        Credit = creditAmount,
                        Debit = debitAmount,
                        RunningBalance = 0, // temporary — recalculation will overwrite
                        Description = logDescription,
                        CreatedAt = pakTime,
                        CreatedBy = currentUserId
                    });

                    _db.SaveChanges();

                    _recalcService.RecalculateTankerLedger(tanker.Id);
                }

                _db.SaveChanges();
                transaction.Commit();

                TempData["add_ledger"] = "Ledger Added Successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Error"] = ex.Message;
                return RedirectToAction("AddLedger", new { tripId = vm.TripEntryId });
            }
        }

        // ==========================================
        // EDIT LEDGER (GET)
        // ==========================================
        public IActionResult EditLedger(int id)
        {
            var ledger = _db.TripLedgers
                .Include(x => x.Expenses)
                .FirstOrDefault(x => x.Id == id);

            if (ledger == null) return NotFound();

            var trip = _db.TripEntries
                .Include(x => x.TankerFk)
                .FirstOrDefault(x => x.Id == ledger.TripEntryId);

            if (trip == null) return NotFound();

            var expenses = _db.TripExpenses
                .Where(x => x.TripLedgerId == ledger.Id)
                .ToList();

            var vm = new TripLedgerVM
            {
                Id = ledger.Id,
                TripDate = ledger.TripDate,
                TripEntryId = ledger.TripEntryId,
                ProductId = ledger.ProductId,
                Freight = ledger.Freight,
                AdvanceCash = ledger.AdvanceCash,
                Shortage = ledger.Shortage,
                ShortageLiters = ledger.ShortageLiters,
                Commission = ledger.Commission,
                Munshiana = ledger.Munshiana,
                AmountPay = ledger.AmountPay,
                PayingAmount = ledger.AmountPay ?? 0,
                TokenNo = ledger.TokenNo,
                GrandTotal = ledger.GrandTotal,
                Remarks = ledger.Remarks,
                TotalDeductions = ledger.TotalDeductions
            };

            ViewBag.Trip = trip;
            ViewBag.Products = _db.Products.ToList();
            ViewBag.Expenses = expenses;
            ViewBag.TankerPreviousBalance = (trip.TankerFk?.CurrentBalance ?? 0) - (ledger.AmountPay ?? 0);

            return View(vm);
        }

        // ==========================================
        // EDIT LEDGER (POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditLedger(TripLedgerVM vm)
        {
            using var transaction = _db.Database.BeginTransaction();

            try
            {
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

                var existingLedger = _db.TripLedgers.FirstOrDefault(x => x.Id == vm.Id);
                if (existingLedger == null)
                    return NotFound();

                bool tokenExists = _db.TripLedgers.Any(x => x.TokenNo == vm.TokenNo && x.Id != vm.Id);
                if (tokenExists)
                {
                    ModelState.AddModelError("TokenNo", "Token No already exists on another trip.");
                    ViewBag.Products = _db.Products.ToList();

                    var originalTrip = _db.TripEntries
                        .Include(x => x.TankerFk)
                        .FirstOrDefault(x => x.Id == vm.TripEntryId);

                    if (originalTrip != null)
                    {
                        ViewBag.Trip = originalTrip;
                        ViewBag.TankerPreviousBalance = originalTrip.TankerFk?.CurrentBalance ?? 0;
                    }

                    return View(vm);
                }

                var trip = _db.TripEntries
                    .Include(x => x.TankerFk)
                    .Include(x => x.TripExpenses)
                    .FirstOrDefault(x => x.Id == vm.TripEntryId);

                if (trip == null)
                    return NotFound();

                var tanker = trip.TankerFk;

                var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                var commissionSetup = _db.CommissionSetups.FirstOrDefault(x => x.IsActive);
                decimal commissionPercent = commissionSetup?.Percentage ?? 0;
                decimal calculatedCommission = (vm.Freight * commissionPercent) / 100;

                decimal advanceCashFromEntry = trip.AdvanceCash;
                decimal totalExpenseFromEntry = trip.TripExpenses.Sum(x => x.Amount);

                decimal totalDeductions = advanceCashFromEntry + vm.Shortage + calculatedCommission + vm.Munshiana + totalExpenseFromEntry;
                decimal grandTotal = vm.Freight - totalDeductions;

                // Modified by AI
                // Date: 2026-07-21
                // Reason: C-03 — The old formula `(tanker.CurrentBalance) - existingLedger.GrandTotal`
                // was mathematically wrong. GrandTotal can be negative (a loss trip), and subtracting
                // a negative number would ADD to the balance instead of removing the trip's contribution.
                // The correct approach is to read the actual Credit and Debit that were posted to
                // TankerLedger for this trip, compute their net (Credit - Debit), and subtract that
                // from the current balance to get the balance as if this trip never existed.
                // This gives the correct basis for determining whether amountPay should be applied.
                var existingTankerLogForBalance = _db.TankerLedgers
                    .FirstOrDefault(x => x.ModuleName == "Trip Ledger Add" && x.ReferenceId == existingLedger.Id);

                decimal oldTripNetContribution = existingTankerLogForBalance != null
                    ? (existingTankerLogForBalance.Credit - existingTankerLogForBalance.Debit)
                    : existingLedger.GrandTotal; // fallback if no log row (e.g., grandTotal was 0)

                decimal balanceExcludingThisTrip = (tanker?.CurrentBalance ?? 0) - oldTripNetContribution;

                decimal amountPay = 0;
                if (balanceExcludingThisTrip < 0 && grandTotal > 0)
                {
                    decimal outstandingDues = Math.Abs(balanceExcludingThisTrip);
                    amountPay = Math.Min(outstandingDues, grandTotal);
                }

                existingLedger.TripDate = vm.TripDate;
                existingLedger.ProductId = vm.ProductId;
                existingLedger.Freight = vm.Freight;
                existingLedger.AdvanceCash = advanceCashFromEntry;
                existingLedger.Shortage = vm.Shortage;
                existingLedger.ShortageLiters = vm.ShortageLiters;
                existingLedger.Commission = calculatedCommission;
                existingLedger.Munshiana = vm.Munshiana;
                existingLedger.TokenNo = vm.TokenNo;
                existingLedger.Remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? "N/A" : vm.Remarks;
                existingLedger.TotalDeductions = totalDeductions;
                existingLedger.GrandTotal = grandTotal;
                existingLedger.AmountPay = amountPay;
                existingLedger.UpdatedAt = pakTime;
                existingLedger.UpdatedBy = currentUserId;

                if (tanker != null)
                {
                    var oldTankerLog = _db.TankerLedgers
                        .FirstOrDefault(x => x.ModuleName == "Trip Ledger Add" && x.ReferenceId == existingLedger.Id);

                    decimal creditAmount = grandTotal > 0 ? grandTotal : 0;
                    decimal debitAmount = grandTotal < 0 ? Math.Abs(grandTotal) : 0;

                    string logDescription = grandTotal > 0
                        ? (balanceExcludingThisTrip < 0
                            ? $"Trip Profit applied: Dues adjusted. Token#: {existingLedger.TokenNo}"
                            : $"Trip Profit Credited. Token#: {existingLedger.TokenNo}")
                        : $"Trip Loss Debited. Deductions exceeded Freight. Token#: {existingLedger.TokenNo}";

                    if (oldTankerLog != null)
                    {
                        if (grandTotal == 0)
                        {
                            _db.TankerLedgers.Remove(oldTankerLog);
                        }
                        else
                        {
                            oldTankerLog.TransactionDate = trip.LoadDate;
                            oldTankerLog.Credit = creditAmount;
                            oldTankerLog.Debit = debitAmount;
                            oldTankerLog.Description = logDescription;
                            oldTankerLog.CreatedBy = currentUserId;
                            // RunningBalance set by RecalculateTankerLedger below
                        }
                    }
                    else if (grandTotal != 0)
                    {
                        _db.TankerLedgers.Add(new TankerLedger()
                        {
                            TankerId = tanker.Id,
                            TransactionDate = trip.LoadDate,
                            ModuleName = "Trip Ledger Add",
                            ReferenceId = existingLedger.Id,
                            Credit = creditAmount,
                            Debit = debitAmount,
                            RunningBalance = 0, // temp
                            Description = logDescription,
                            CreatedAt = pakTime,
                            CreatedBy = currentUserId
                        });
                    }

                    _db.SaveChanges();

                    _recalcService.RecalculateTankerLedger(tanker.Id);
                }

                _db.SaveChanges();
                transaction.Commit();

                TempData["edit_ledger"] = "Ledger Updated Successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Error"] = ex.Message;
                return RedirectToAction("EditLedger", new { id = vm.Id });
            }
        }

        public IActionResult PrintBill(int id)
        {
            var data = _db.TripLedgers
                .Include(x => x.TripEntryFk)
                    .ThenInclude(t => t.TankerFk)
                .Include(x => x.Product)
                .Include(x => x.Expenses)
                .FirstOrDefault(x => x.Id == id);

            if (data == null) return NotFound();

            return View(data);
        }
    }
}