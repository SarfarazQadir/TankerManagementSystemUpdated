using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Models;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AuthorizeModule]
    public class TripEntryController : Controller
    {
        private readonly ApplicationDbContext _db;

        public TripEntryController(ApplicationDbContext db)
        {
            _db = db;
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

        // ADD POST
        /* [HttpPost]
         public IActionResult AddEntry(TripEntry request)
         {
             var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
             request.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);

             var sessionValue = HttpContext.Session.GetString("admin_session");

             if (!string.IsNullOrEmpty(sessionValue))
                 request.CreatedBy = int.Parse(sessionValue);
             else
                 return RedirectToAction("Login", "Admin");

             if (request.TankerId == 0 || string.IsNullOrWhiteSpace(request.ToLocation))
             {
                 TempData["Error"] = "Required fields missing";
                 return RedirectToAction("AddEntry");
             }

             _db.TripEntries.Add(request);
             _db.SaveChanges();

             TempData["add_trip_message"] = "Trip added successfully.";
             return RedirectToAction("Index");
         }
 */

        // ADD POST
        /* [HttpPost]
         public IActionResult AddEntry(TripEntry request)
         {
             var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
             request.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);

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
                 request.CreatedBy = currentUserId;

             if (request.TankerId == 0 || string.IsNullOrWhiteSpace(request.ToLocation))
             {
                 TempData["Error"] = "Required fields missing";
                 return RedirectToAction("AddEntry");
             }

             // CHECK DUPLICATE ENTRY
             bool alreadyExists = _db.TripEntries.Any(x =>
                 x.TankerId == request.TankerId &&
                 x.FromLocation == request.FromLocation &&
                 x.ToLocation == request.ToLocation &&
                 x.CreatedAt.Date == request.CreatedAt.Date
             );

             if (alreadyExists)
             {
                 TempData["Error"] = "Same tanker already has an entry for this route on the same date.";

                 // TOASTER MESSAGE
                 TempData["toast_error"] = "Duplicate entry not allowed.";

                 return RedirectToAction("AddEntry");
             }

             _db.TripEntries.Add(request);
             _db.SaveChanges();

             TempData["add_trip_message"] = "Trip added successfully.";
             return RedirectToAction("Index");
         }
        */

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

                // CHECK DUPLICATE ENTRY
                bool alreadyExists = _db.TripEntries.Any(x =>
                    x.TankerId == request.TankerId &&
                    x.FromLocation == request.FromLocation &&
                    x.ToLocation == request.ToLocation &&
                    x.CreatedAt.Date == request.CreatedAt.Date
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
                var cashLedger = _db.CashLedgers.OrderByDescending(x => x.Id).FirstOrDefault();
                decimal currentCashBalance = cashLedger?.Balance ?? 0;

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
                        EntryDate = pakTime,
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
                            EntryDate = pakTime,
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



        // EDIT POST
        /*[HttpPost]
        public IActionResult EditEntry(TripEntry update)
        {
            var trip = _db.TripEntries.FirstOrDefault(x => x.Id == update.Id);
            if (trip == null) return NotFound();

            var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            trip.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);

            trip.LoadDate = update.LoadDate;
            trip.TankerId = update.TankerId;
            trip.AdvanceCash = update.AdvanceCash;
            trip.FromLocation = update.FromLocation;
            trip.ToLocation = update.ToLocation;

            var sessionValue = HttpContext.Session.GetString("admin_session");
            if (!string.IsNullOrEmpty(sessionValue))
                trip.UpdatedBy = int.Parse(sessionValue);
            else
                return RedirectToAction("Login", "Admin");

            _db.SaveChanges();

            TempData["edit_trip_message"] = "Trip updated successfully.";
            return RedirectToAction("Index");
        }*/

        //Working Start

        // EDIT GET
        /* public IActionResult EditEntry(int id)
         {
             var trip = _db.TripEntries.FirstOrDefault(x => x.Id == id);
             if (trip == null) return NotFound();

             ViewBag.Tankers = _db.Tankers.ToList();
             return View(trip);
         }

         // EDIT POST
         [HttpPost]
         public IActionResult EditEntry(TripEntry update)
         {
             var trip = _db.TripEntries.FirstOrDefault(x => x.Id == update.Id);
             if (trip == null) return NotFound();

             // CHECK DUPLICATE ENTRY
             bool alreadyExists = _db.TripEntries.Any(x =>
                 x.Id != update.Id && // current record exclude
                 x.TankerId == update.TankerId &&
                 x.FromLocation == update.FromLocation &&
                 x.ToLocation == update.ToLocation &&
                 x.LoadDate.Date == update.LoadDate.Date
             );

             if (alreadyExists)
             {
                 TempData["Error"] = "Same tanker already has an entry for this route on the same date.";
                 TempData["toast_error"] = "Duplicate entry not allowed.";

                 return RedirectToAction("EditEntry", new { id = update.Id });
             }

             var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
             trip.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);

             trip.LoadDate = update.LoadDate;
             trip.TankerId = update.TankerId;
             trip.AdvanceCash = update.AdvanceCash;
             trip.FromLocation = update.FromLocation;
             trip.ToLocation = update.ToLocation;

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

             trip.UpdatedBy = currentUserId;

             _db.SaveChanges();

             TempData["edit_trip_message"] = "Trip updated successfully.";
             return RedirectToAction("Index");
         }
 */
        //Working End

        // EDIT GET
        public IActionResult EditEntry(int id)
        {
            var trip = _db.TripEntries
                .Include(x => x.TripExpenses) // Expenses load kiye
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

                // =========================================================================
                // 🔥 STEP A: REVERSE OLD CASH DEDUCTIONS (To prevent double deductions)
                // =========================================================================
                var latestCash = _db.CashLedgers.OrderByDescending(x => x.Id).FirstOrDefault();
                decimal currentCashBalance = latestCash?.Balance ?? 0;

                // Purana Advance Cash wapas balance me add karein
                if (trip.AdvanceCash > 0)
                {
                    currentCashBalance += trip.AdvanceCash;
                    _db.CashLedgers.Add(new CashLedger()
                    {
                        EntryDate = pakTime,
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
                        EntryDate = pakTime,
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
                    // Transaction rollback ho jayegi automatic agar yahan se return hua to
                    TempData["Error"] = $"Insufficient cash balance for these updates. Required: {netNewCashRequired}, Available: {currentCashBalance}";
                    return RedirectToAction("EditEntry", new { id = update.Id });
                }

                // =========================================================================
                // 🔥 STEP B: APPLY UPDATED VALUES & DEDUCTIONS
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
                        EntryDate = pakTime,
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
                        EntryDate = pakTime,
                        Description = $"[UPDATED] Trip Expense Log: {item.ExpenseName} | Trip Entry Ref: {trip.Id}",
                        Debit = item.Amount,
                        Credit = 0,
                        Balance = currentCashBalance,
                        CreatedAt = pakTime,
                        CreatedBy = currentUserId
                    });
                }

                _db.SaveChanges();
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