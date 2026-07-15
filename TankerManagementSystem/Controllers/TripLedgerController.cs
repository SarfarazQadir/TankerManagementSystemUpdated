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

        // ==========================================
        // 🔥 SHARED HELPER: Recalculate RunningBalance
        // Har add/edit/delete ke baad ye method us tanker ki
        // saari TankerLedgers rows ko TransactionDate order mein
        // dobara chain kar deta hai, taake RunningBalance hamesha
        // sahi sequence mein rahe (chahe entry backdated ho).
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
                tanker.CurrentBalance = running; // final balance sequence ke hisab se
            }

            _db.SaveChanges();
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
                TripDate = trip.LoadDate, // 🔥 FIX: pehle DateTime.Now tha, ab trip ki asal LoadDate
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

                // Ye "server insertion time" ke liye chahiye (CreatedAt audit field), TransactionDate ke liye nahi
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
                        TransactionDate = trip.LoadDate, // 🔥 FIX: pakTime ki jagah asal trip ki date
                        ModuleName = "Trip Ledger Add",
                        ReferenceId = model.Id,
                        Credit = creditAmount,
                        Debit = debitAmount,
                        RunningBalance = 0, // temporary — recalculation se overwrite ho jayega
                        Description = logDescription,
                        CreatedAt = pakTime,
                        CreatedBy = currentUserId
                    });

                    _db.SaveChanges();

                    // 🔥 FIX: Insert ke baad turant is tanker ki poori chain date-order mein recalc karo
                    RecalculateTankerLedger(tanker.Id);
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

                // NOTE: Ab hum tanker.CurrentBalance ko manually +/- nahi karenge —
                // RecalculateTankerLedger() sab kuch date-order se khud theek kar dega.

                var commissionSetup = _db.CommissionSetups.FirstOrDefault(x => x.IsActive);
                decimal commissionPercent = commissionSetup?.Percentage ?? 0;
                decimal calculatedCommission = (vm.Freight * commissionPercent) / 100;

                decimal advanceCashFromEntry = trip.AdvanceCash;
                decimal totalExpenseFromEntry = trip.TripExpenses.Sum(x => x.Amount);

                decimal totalDeductions = advanceCashFromEntry + vm.Shortage + calculatedCommission + vm.Munshiana + totalExpenseFromEntry;
                decimal grandTotal = vm.Freight - totalDeductions;

                // Purani row ko chhod ke, is tanker ka balance kya tha (reference ke liye — dues-adjustment logic)
                decimal balanceExcludingThisTrip = (tanker?.CurrentBalance ?? 0) - existingLedger.GrandTotal;

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
                    // Purani TankerLedger row dhoondo aur usko UPDATE karo (delete+re-add ki jagah)
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
                            oldTankerLog.TransactionDate = trip.LoadDate; // 🔥 FIX: asal trip date
                            oldTankerLog.Credit = creditAmount;
                            oldTankerLog.Debit = debitAmount;
                            oldTankerLog.Description = logDescription;
                            oldTankerLog.CreatedBy = currentUserId;
                            // RunningBalance yahan set nahi karna — RecalculateTankerLedger() karega
                        }
                    }
                    else if (grandTotal != 0)
                    {
                        _db.TankerLedgers.Add(new TankerLedger()
                        {
                            TankerId = tanker.Id,
                            TransactionDate = trip.LoadDate, // 🔥 FIX
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

                    // 🔥 FIX: Edit ke baad pura chain date-order mein recalc — Bug #3 aur #4 dono fix
                    RecalculateTankerLedger(tanker.Id);
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