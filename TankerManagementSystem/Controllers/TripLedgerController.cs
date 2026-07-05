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
    public class TripLedgerController : Controller
    {
        private readonly ApplicationDbContext _db;

        public TripLedgerController(ApplicationDbContext db)
        {
            _db = db;
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
            // Check if ledger already exists for this trip
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

            // Naye design ke mutabiq Tanker ka CurrentBalance hi ab screen par show hoga
            // Agar balance null ya 0 ho to view handle kar lega, yahan hum direct decimal pass kar rahe hain
            ViewBag.TankerPreviousBalance = trip.TankerFk?.CurrentBalance ?? 0;

            // 🔥 FIX: ViewModel ko initialize karke values assign karein taake View me auto-fetch ho sake
            var model = new TankerManagementSystem.Models.ViewModels.TripLedgerVM
            {
                TripEntryId = trip.Id,
                TripDate = DateTime.Now, // Default current date layout

                // Yeh line TripEntry se AdvanceCash utha kar direct field me map kar degi
                AdvanceCash = trip.AdvanceCash,

                // default settings for numeric fields
                Freight = 0,
                Shortage = 0,
                Commission = 0,
                Munshiana = 0
            };

            // 🔥 Model ko view ke andar pass karna zaroori hai
            return View(model);
        }
        //Working End

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddLedger(TripLedgerVM vm)
        {
            using var transaction = _db.Database.BeginTransaction();
            try
            {
                // 1. Session Check First
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

                // 2. Token Unique Check
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

                // 3. Fetch Trip along with its pre-saved Expenses
                var trip = _db.TripEntries
                    .Include(x => x.TankerFk)
                    .Include(x => x.TripExpenses) // Expenses include kiye
                    .FirstOrDefault(x => x.Id == vm.TripEntryId);

                if (trip == null) return NotFound();
                var tanker = trip.TankerFk;

                // 4. Time Setup
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                // 5. Automatic Commission Fetching & Calculations
                var commissionSetup = _db.CommissionSetups.FirstOrDefault(x => x.IsActive);
                decimal commissionPercent = commissionSetup?.Percentage ?? 0;
                decimal calculatedCommission = (vm.Freight * commissionPercent) / 100;

                // 🔥 NO DOUBLE ENTRY FIX: Values fetched directly from Trip Entry
                decimal advanceCashFromEntry = trip.AdvanceCash;
                decimal totalExpenseFromEntry = trip.TripExpenses.Sum(x => x.Amount);

                // Calculate Grand Total (Trip Profit / Loss) using pre-deducted amounts
                decimal totalDeductions = advanceCashFromEntry + vm.Shortage + calculatedCommission + vm.Munshiana + totalExpenseFromEntry;
                decimal grandTotal = vm.Freight - totalDeductions;

                // --- Cash Ledger Balance Validation is REMOVED from here because cash was already deducted in Trip Entry ---

                decimal calculatedAmountPay = 0;
                decimal oldBalance = tanker?.CurrentBalance ?? 0;

                if (oldBalance < 0 && grandTotal > 0)
                {
                    decimal outstandingDues = Math.Abs(oldBalance);
                    calculatedAmountPay = Math.Min(outstandingDues, grandTotal);
                }

                // 7. Map & Save Trip Ledger Entity
                var model = new TripLedger()
                {
                    TripDate = vm.TripDate,
                    TripEntryId = vm.TripEntryId,
                    ProductId = vm.ProductId,
                    Freight = vm.Freight,
                    AdvanceCash = advanceCashFromEntry, // Saved what was actually paid in Trip Entry
                    Shortage = vm.Shortage,
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

                // 🔥 Update pre-saved expenses to link them with this Trip Ledger ID (For reports)
                foreach (var exp in trip.TripExpenses)
                {
                    exp.TripLedgerId = model.Id;
                    _db.Entry(exp).State = EntityState.Modified;
                }

                // 8. Update Tanker Balance & Log Ledger Entries
                if (tanker != null)
                {
                    decimal newBalance = oldBalance + grandTotal;
                    tanker.CurrentBalance = newBalance;

                    if (grandTotal != 0)
                    {
                        decimal creditAmount = grandTotal > 0 ? grandTotal : 0;
                        decimal debitAmount = grandTotal < 0 ? Math.Abs(grandTotal) : 0;

                        string logDescription = grandTotal > 0
                            ? (oldBalance < 0 ? $"Trip Profit applied: Dues adjusted. Token#: {model.TokenNo}" : $"Trip Profit Credited. Token#: {model.TokenNo}")
                            : $"Trip Loss Debited. Deductions exceeded Freight. Token#: {model.TokenNo}";

                        _db.TankerLedgers.Add(new TankerLedger()
                        {
                            TankerId = tanker.Id,
                            TransactionDate = pakTime,
                            ModuleName = "Trip Ledger Add",
                            ReferenceId = model.Id,
                            Credit = creditAmount,
                            Debit = debitAmount,
                            RunningBalance = newBalance,
                            Description = logDescription,
                            CreatedAt = pakTime,
                            CreatedBy = currentUserId
                        });
                    }
                }

                // ❌ CASH LEDGER LOGS REMOVED FROM HERE COMPLETELY TO PREVENT DOUBLE ENTRY.

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

            // Aapka reverse math logic bilkul perfect tha!
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
                // 1. Session Check
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

                // 2. Fetch Existing Ledger
                var existingLedger = _db.TripLedgers
                    .FirstOrDefault(x => x.Id == vm.Id);

                if (existingLedger == null)
                    return NotFound();

                // 3. Token Unique Check
                bool tokenExists = _db.TripLedgers
                    .Any(x => x.TokenNo == vm.TokenNo && x.Id != vm.Id);

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

                // 4. Fetch Trip With Expenses
                var trip = _db.TripEntries
                    .Include(x => x.TankerFk)
                    .Include(x => x.TripExpenses)
                    .FirstOrDefault(x => x.Id == vm.TripEntryId);

                if (trip == null)
                    return NotFound();

                var tanker = trip.TankerFk;

                // 5. Pakistan Time
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                // =====================================================
                // REVERSE OLD EFFECT
                // =====================================================

                decimal oldGrandTotal = existingLedger.GrandTotal;

                if (tanker != null)
                {
                    tanker.CurrentBalance -= oldGrandTotal;
                }

                // =====================================================
                // CALCULATIONS (SAME AS ADD LEDGER)
                // =====================================================

                var commissionSetup = _db.CommissionSetups
                    .FirstOrDefault(x => x.IsActive);

                decimal commissionPercent = commissionSetup?.Percentage ?? 0;

                decimal calculatedCommission =
                    (vm.Freight * commissionPercent) / 100;

                // SAME VALUES USED IN ADD LEDGER
                decimal advanceCashFromEntry = trip.AdvanceCash;

                decimal totalExpenseFromEntry =
                    trip.TripExpenses.Sum(x => x.Amount);

                decimal totalDeductions =
                    advanceCashFromEntry
                    + vm.Shortage
                    + calculatedCommission
                    + vm.Munshiana
                    + totalExpenseFromEntry;

                decimal grandTotal =
                    vm.Freight - totalDeductions;

                decimal amountPay = 0;

                decimal oldBalanceWithoutThisTrip =
                    tanker?.CurrentBalance ?? 0;

                if (oldBalanceWithoutThisTrip < 0 && grandTotal > 0)
                {
                    decimal outstandingDues =
                        Math.Abs(oldBalanceWithoutThisTrip);

                    amountPay =
                        Math.Min(outstandingDues, grandTotal);
                }

                // =====================================================
                // UPDATE LEDGER
                // =====================================================

                existingLedger.TripDate = vm.TripDate;
                existingLedger.ProductId = vm.ProductId;
                existingLedger.Freight = vm.Freight;

                existingLedger.AdvanceCash = advanceCashFromEntry;

                existingLedger.Shortage = vm.Shortage;
                existingLedger.Commission = calculatedCommission;
                existingLedger.Munshiana = vm.Munshiana;
                existingLedger.TokenNo = vm.TokenNo;

                existingLedger.Remarks =
                    string.IsNullOrWhiteSpace(vm.Remarks)
                        ? "N/A"
                        : vm.Remarks;

                existingLedger.TotalDeductions = totalDeductions;
                existingLedger.GrandTotal = grandTotal;
                existingLedger.AmountPay = amountPay;

                existingLedger.UpdatedAt = pakTime;
                existingLedger.UpdatedBy = currentUserId;

                // =====================================================
                // UPDATE TANKER BALANCE
                // =====================================================

                if (tanker != null)
                {
                    tanker.CurrentBalance += grandTotal;

                    // Remove Old Tanker Log
                    var oldTankerLog = _db.TankerLedgers
                        .FirstOrDefault(x =>
                            x.ModuleName == "Trip Ledger Add" &&
                            x.ReferenceId == existingLedger.Id);

                    if (oldTankerLog != null)
                    {
                        _db.TankerLedgers.Remove(oldTankerLog);
                    }

                    if (grandTotal != 0)
                    {
                        decimal creditAmount =
                            grandTotal > 0 ? grandTotal : 0;

                        decimal debitAmount =
                            grandTotal < 0 ? Math.Abs(grandTotal) : 0;

                        string logDescription =
                            grandTotal > 0
                            ? (oldBalanceWithoutThisTrip < 0
                                ? $"Trip Profit applied: Dues adjusted. Token#: {existingLedger.TokenNo}"
                                : $"Trip Profit Credited. Token#: {existingLedger.TokenNo}")
                            : $"Trip Loss Debited. Deductions exceeded Freight. Token#: {existingLedger.TokenNo}";

                        _db.TankerLedgers.Add(new TankerLedger()
                        {
                            TankerId = tanker.Id,
                            TransactionDate = pakTime,
                            ModuleName = "Trip Ledger Add",
                            ReferenceId = existingLedger.Id,
                            Credit = creditAmount,
                            Debit = debitAmount,
                            RunningBalance = tanker.CurrentBalance,
                            Description = logDescription,
                            CreatedAt = pakTime,
                            CreatedBy = currentUserId
                        });
                    }
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