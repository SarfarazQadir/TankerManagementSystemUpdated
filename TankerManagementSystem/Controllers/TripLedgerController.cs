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

        // LIST
        /*  public IActionResult Index()
          {
              var data = _db.TripLedgers
                  .Include(x => x.Product)
                  .Include(x => x.TripEntryFk)
                  .OrderByDescending(x => x.Id)
                  .ToList();

              return View(data);
          }*/

        // ADD GET
        /*   public IActionResult AddLedger(int tripId)
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
               ViewBag.TankerPreviousBalance = trip.TankerFk?.PreviousBalance ?? 0;

               return View();
           }
   */
        /* [HttpPost]
         [ValidateAntiForgeryToken]
         public IActionResult AddLedger(TripLedgerVM vm, List<TripExpense> expenses)
         {
             // 1. JWT COOKIE SE USER ID NIKALNA
             // =========================================
             // Agar user logged in nahi hai, toh User.FindFirst null dega
             // Try multiple claim types and fall back to Identity.Name to handle different auth setups (cookies, JWT, external providers)
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

             // =========================
             // CREATE ENTITY
             // =========================
             var model = new TripLedger
             {
                 TripDate = vm.TripDate,
                 TripEntryId = vm.TripEntryId,
                 ProductId = vm.ProductId,
                 Freight = vm.Freight,
                 AdvanceCash = vm.AdvanceCash,
                 Shortage = vm.Shortage,
                 Commission = vm.Commission,
                 Munshiana = vm.Munshiana,
                 TokenNo = vm.TokenNo,
                 Remarks = vm.Remarks
             };

             // =========================
             // PAKISTAN TIME
             // =========================
             var tz = TimeZoneInfo.FindSystemTimeZoneById(
                 "Pakistan Standard Time");

             DateTime pakTime =
                 TimeZoneInfo.ConvertTimeFromUtc(
                     DateTime.UtcNow,
                     tz);

             model.CreatedAt = pakTime;
             model.CreatedBy = currentUserId;

             // =========================
             // TOKEN CHECK
             // =========================
             bool tokenExists = _db.TripLedgers
                 .Any(x => x.TokenNo == model.TokenNo);

             if (tokenExists)
             {
                 ModelState.AddModelError(
                     "TokenNo",
                     "Token No already exists."
                 );

                 ViewBag.Products = _db.Products.ToList();

                 return View(vm);
             }

             // =========================
             // FETCH TRIP + TANKER
             // =========================
             var trip = _db.TripEntries
                 .Include(x => x.TankerFk)
                 .FirstOrDefault(x => x.Id == model.TripEntryId);

             if (trip == null)
             {
                 return NotFound();
             }

             var tanker = trip.TankerFk;

             // =========================
             // VIEWBAG
             // =========================
             ViewBag.Trip = trip;

             ViewBag.Products =
                 _db.Products.ToList();

             ViewBag.TankerPreviousBalance =
                 tanker?.PreviousBalance ?? 0;

             // =========================
             // OLD BALANCE
             // =========================
             decimal oldBalance =
                 tanker?.PreviousBalance ?? 0;

             // =========================
             // DEFAULT REMARKS
             // =========================
             if (string.IsNullOrWhiteSpace(model.Remarks))
             {
                 model.Remarks = "N/A";
             }

             // =========================
             // COMMISSION %
             // =========================
             var commissionSetup =
                 _db.CommissionSetups
                 .FirstOrDefault(x => x.IsActive);

             decimal commissionPercent =
                 commissionSetup?.Percentage ?? 0;

             // =========================
             // COMMISSION CALCULATE
             // =========================
             model.Commission =
                 (model.Freight * commissionPercent) / 100;

             // =========================
             // CLEAN EXPENSES
             // =========================
             if (expenses == null)
             {
                 expenses = new List<TripExpense>();
             }

             var uniqueExpenses = expenses
                 .Where(x =>
                     !string.IsNullOrWhiteSpace(x.ExpenseName)
                     && x.Amount > 0
                 )
                 .GroupBy(x => new
                 {
                     ExpenseName = x.ExpenseName
                         .Trim()
                         .ToLower(),

                     x.Amount
                 })
                 .Select(g => g.First())
                 .ToList();

             // =========================
             // TOTAL EXPENSE
             // =========================
             decimal totalExpense =
                 uniqueExpenses.Sum(x => x.Amount);

             // =========================
             // TOTAL DEDUCTIONS
             // =========================
             decimal totalDeductions =
                 model.AdvanceCash
                 + model.Shortage
                 + model.Commission
                 + model.Munshiana
                 + totalExpense;

             model.TotalDeductions =
                 totalDeductions;

             // =========================
             // GRAND TOTAL
             // =========================
             decimal grandTotal =
                 model.Freight - totalDeductions;

             model.GrandTotal =
                 grandTotal;

             // =========================
             // DEFAULT
             // =========================
             decimal newBalance = oldBalance;

             model.AmountPay = 0;

             // =========================================
             // PROFIT CASE
             // =========================================
             if (grandTotal > 0)
             {
                 // =====================================
                 // IF PREVIOUS BALANCE EXISTS
                 // =====================================
                 if (oldBalance > 0)
                 {
                     // =====================================
                     // PROFIT <= PREVIOUS BALANCE
                     //
                     // Example:
                     // Previous = 300000
                     // Profit = 100000
                     // Remaining = 200000
                     // AmountPay = 100000
                     // =====================================
                     if (grandTotal <= oldBalance)
                     {
                         model.AmountPay =
                             grandTotal;

                         tanker.PreviousBalance =
                             oldBalance - grandTotal;

                         newBalance =
                             tanker.PreviousBalance;

                         TankerBalanceLog adjustLog =
                             new TankerBalanceLog()
                             {
                                 TankerId = tanker.Id,

                                 ReferenceId = model.Id,

                                 EntryDate = pakTime,

                                 TransactionType = "DEDUCT",

                                 ModuleName = "Trip Ledger Profit Adjustment",

                                 Amount = grandTotal,

                                 PreviousBalance = oldBalance,

                                 NewBalance = newBalance,

                                 Description =
                                     $"Profit adjusted against previous tanker balance. Paid Amount: {grandTotal}",

                                 CreatedAt = pakTime,

                                 CreatedBy = currentUserId
                             };

                         _db.TankerBalanceLogs.Add(adjustLog);
                     }
                     else
                     {
                         // =====================================
                         // PROFIT > PREVIOUS BALANCE
                         //
                         // Example:
                         // Previous = 50000
                         // Profit = 100000
                         //
                         // Balance Cleared = 50000
                         // Extra Profit = 50000
                         // =====================================

                         decimal clearedAmount =
                             oldBalance;

                         decimal remainingProfit =
                             grandTotal - oldBalance;

                         // =========================
                         // STORE CLEARED AMOUNT
                         // =========================
                         model.AmountPay =
                             clearedAmount;

                         tanker.PreviousBalance = 0;

                         newBalance = 0;

                         TankerBalanceLog clearBalanceLog =
                             new TankerBalanceLog()
                             {
                                 TankerId = tanker.Id,

                                 ReferenceId = model.Id,

                                 EntryDate = pakTime,

                                 TransactionType = "DEDUCT",

                                 ModuleName = "Trip Ledger Balance Clear",

                                 Amount = clearedAmount,

                                 PreviousBalance = oldBalance,

                                 NewBalance = 0,

                                 Description =
                                     $"Previous tanker balance fully cleared from trip profit.",

                                 CreatedAt = pakTime,

                                 CreatedBy = currentUserId
                             };

                         _db.TankerBalanceLogs.Add(clearBalanceLog);

                         // =========================
                         // EXTRA PROFIT LOG
                         // =========================
                         if (remainingProfit > 0)
                         {
                             TankerBalanceLog extraProfitLog =
                                 new TankerBalanceLog()
                                 {
                                     TankerId = tanker.Id,

                                     ReferenceId = model.Id,

                                     EntryDate = pakTime,

                                     TransactionType = "ADD",

                                     ModuleName = "Trip Ledger Extra Profit",

                                     Amount = remainingProfit,

                                     PreviousBalance = 0,

                                     NewBalance = 0,

                                     Description =
                                         $"Extra profit generated after clearing tanker balance. Extra Profit: {remainingProfit}",

                                     CreatedAt = pakTime,

                                     CreatedBy = currentUserId
                                 };

                             _db.TankerBalanceLogs.Add(extraProfitLog);
                         }
                     }
                 }
                 else
                 {
                     // =====================================
                     // NO PREVIOUS BALANCE
                     // FULL PROFIT
                     // =====================================

                     model.AmountPay =
                         grandTotal;

                     TankerBalanceLog profitLog =
                         new TankerBalanceLog()
                         {
                             TankerId = tanker.Id,

                             ReferenceId = model.Id,

                             EntryDate = pakTime,

                             TransactionType = "ADD",

                             ModuleName = "Trip Ledger Profit",

                             Amount = grandTotal,

                             PreviousBalance = oldBalance,

                             NewBalance = oldBalance,

                             Description =
                                 $"Trip profit generated. Profit Amount: {grandTotal}",

                             CreatedAt = pakTime,

                             CreatedBy = currentUserId
                         };

                     _db.TankerBalanceLogs.Add(profitLog);
                 }
             }

             // =========================================
             // LOSS CASE
             // =========================================
             else if (grandTotal < 0)
             {
                 decimal lossAmount =
                     Math.Abs(grandTotal);

                 tanker.PreviousBalance =
                     oldBalance + lossAmount;

                 newBalance =
                     tanker.PreviousBalance;

                 model.AmountPay = 0;

                 TankerBalanceLog lossLog =
                     new TankerBalanceLog()
                     {
                         TankerId = tanker.Id,

                         ReferenceId = model.Id,

                         EntryDate = pakTime,

                         TransactionType = "ADD",

                         ModuleName = "Trip Ledger Loss",

                         Amount = lossAmount,

                         PreviousBalance = oldBalance,

                         NewBalance = newBalance,

                         Description =
                             $"Loss added into tanker pending balance. Loss Amount: {lossAmount}",

                         CreatedAt = pakTime,

                         CreatedBy = currentUserId
                     };

                 _db.TankerBalanceLogs.Add(lossLog);
             }

             // =========================
             // SAVE LEDGER
             // =========================
             _db.TripLedgers.Add(model);

             _db.SaveChanges();

             // =========================
             // SAVE EXPENSES
             // =========================
             if (uniqueExpenses.Count > 0)
             {
                 foreach (var item in uniqueExpenses)
                 {
                     item.TripLedgerId =
                         model.Id;

                     item.CreatedAt =
                         pakTime;

                     item.CreatedBy =
                         currentUserId;

                     _db.TripExpenses.Add(item);
                 }

                 _db.SaveChanges();
             }

             // =========================
             // SAVE LOGS
             // =========================
             _db.SaveChanges();

             // =========================
             // SUCCESS
             // =========================
             TempData["add_ledger"] =
                 "Ledger Added Successfully";

             return RedirectToAction("Index");
         }
       */

        // working 👍

        /*   [HttpPost]
           [ValidateAntiForgeryToken]
           public IActionResult AddLedger(
           TripLedgerVM vm,
           List<TripExpense> expenses)
           {
               using var transaction =
                   _db.Database.BeginTransaction();

               try
               {
                   // =========================
                   // CREATE ENTITY
                   // =========================
                   var model = new TripLedger()
                   {
                       TripDate = vm.TripDate,
                       TripEntryId = vm.TripEntryId,
                       ProductId = vm.ProductId,
                       Freight = vm.Freight,
                       AdvanceCash = vm.AdvanceCash,
                       Shortage = vm.Shortage,
                       Commission = vm.Commission,
                       Munshiana = vm.Munshiana,
                       TokenNo = vm.TokenNo,
                       Remarks = vm.Remarks
                   };

                   // =========================
                   // PAKISTAN TIME
                   // =========================
                   var tz =
                       TimeZoneInfo.FindSystemTimeZoneById(
                           "Pakistan Standard Time");

                   DateTime pakTime =
                       TimeZoneInfo.ConvertTimeFromUtc(
                           DateTime.UtcNow,
                           tz);

                   model.CreatedAt = pakTime;

                   // =========================
                   // SESSION CHECK
                   // =========================
                   // 1. JWT COOKIE SE USER ID NIKALNA
                   // =========================================
                   // Agar user logged in nahi hai, toh User.FindFirst null dega
                   // Try multiple claim types and fall back to Identity.Name to handle different auth setups (cookies, JWT, external providers)
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

                   model.CreatedBy = currentUserId;

                   // =========================
                   // TOKEN CHECK
                   // =========================
                   bool tokenExists =
                       _db.TripLedgers
                       .Any(x => x.TokenNo == model.TokenNo);

                   if (tokenExists)
                   {
                       ModelState.AddModelError(
                           "TokenNo",
                           "Token No already exists.");

                       ViewBag.Products =
                           _db.Products.ToList();

                       return View(vm);
                   }

                   // =========================
                   // FETCH TRIP + TANKER
                   // =========================
                   var trip =
                       _db.TripEntries
                       .Include(x => x.TankerFk)
                       .FirstOrDefault(
                           x => x.Id == model.TripEntryId);

                   if (trip == null)
                   {
                       return NotFound();
                   }

                   var tanker = trip.TankerFk;

                   // =========================
                   // VIEWBAG
                   // =========================
                   ViewBag.Trip = trip;

                   ViewBag.Products =
                       _db.Products.ToList();

                   ViewBag.TankerPreviousBalance =
                       tanker?.PreviousBalance ?? 0;

                   // =========================
                   // OLD BALANCE
                   // =========================
                   decimal oldBalance =
                       tanker?.PreviousBalance ?? 0;

                   // =========================
                   // DEFAULT REMARKS
                   // =========================
                   if (string.IsNullOrWhiteSpace(
                       model.Remarks))
                   {
                       model.Remarks = "N/A";
                   }

                   // =========================
                   // COMMISSION %
                   // =========================
                   var commissionSetup =
                       _db.CommissionSetups
                       .FirstOrDefault(x => x.IsActive);

                   decimal commissionPercent =
                       commissionSetup?.Percentage ?? 0;

                   // =========================
                   // COMMISSION CALCULATE
                   // =========================
                   model.Commission =
                       (model.Freight * commissionPercent)
                       / 100;

                   // =========================
                   // CLEAN EXPENSES
                   // =========================
                   if (expenses == null)
                   {
                       expenses =
                           new List<TripExpense>();
                   }

                   var uniqueExpenses =
                       expenses
                       .Where(x =>
                           !string.IsNullOrWhiteSpace(
                               x.ExpenseName)
                           &&
                           x.Amount > 0
                       )
                       .GroupBy(x => new
                       {
                           ExpenseName =
                               x.ExpenseName
                               .Trim()
                               .ToLower(),

                           x.Amount
                       })
                       .Select(g => g.First())
                       .ToList();

                   // =========================
                   // TOTAL EXPENSE
                   // =========================
                   decimal totalExpense =
                       uniqueExpenses.Sum(
                           x => x.Amount);

                   // =========================
                   // TOTAL CASH REQUIRED
                   // =========================
                   decimal totalCashRequired =
                       model.AdvanceCash
                       + model.Shortage
                       + model.Munshiana
                       + totalExpense;

                   // =========================
                   // CASH LEDGER CHECK
                   // =========================
                   var cashLedger =
                       _db.CashLedgers
                       .OrderByDescending(x => x.Id)
                       .FirstOrDefault();

                   decimal currentCashBalance =
                       cashLedger?.Balance ?? 0;

                   // =========================
                   // IF BALANCE LOW
                   // =========================
                   if (currentCashBalance
                       < totalCashRequired)
                   {
                       TempData["Error"] =
                           $"Cash Ledger balance is low. Available Balance: {currentCashBalance}";

                       return RedirectToAction(
                           "AddLedger",
                           new
                           {
                               tripId = vm.TripEntryId
                           });
                   }

                   // =========================
                   // TOTAL DEDUCTIONS
                   // =========================
                   decimal totalDeductions =
                       model.AdvanceCash
                       + model.Shortage
                       + model.Commission
                       + model.Munshiana
                       + totalExpense;

                   model.TotalDeductions =
                       totalDeductions;

                   // =========================
                   // GRAND TOTAL
                   // =========================
                   decimal grandTotal =
                       model.Freight
                       - totalDeductions;

                   model.GrandTotal =
                       grandTotal;

                   // =========================
                   // DEFAULT
                   // =========================
                   decimal newBalance =
                       oldBalance;

                   model.AmountPay = 0;

                   // =========================================
                   // PROFIT CASE
                   // =========================================
                   if (grandTotal > 0)
                   {
                       // =====================================
                       // IF PREVIOUS BALANCE EXISTS
                       // =====================================
                       if (oldBalance > 0)
                       {
                           // =====================================
                           // PROFIT <= PREVIOUS BALANCE
                           // =====================================
                           if (grandTotal <= oldBalance)
                           {
                               model.AmountPay =
                                   grandTotal;

                               tanker.PreviousBalance =
                                   oldBalance - grandTotal;

                               newBalance =
                                   tanker.PreviousBalance;

                               TankerBalanceLog adjustLog =
                                   new TankerBalanceLog()
                                   {
                                       TankerId = tanker.Id,
                                       EntryDate = pakTime,
                                       TransactionType = "DEDUCT",
                                       ModuleName =
                                           "Trip Ledger Profit Adjustment",
                                       Amount = grandTotal,
                                       PreviousBalance = oldBalance,
                                       NewBalance = newBalance,
                                       Description =
                                           $"Profit adjusted against previous tanker balance. Paid Amount: {grandTotal}",
                                       CreatedAt = pakTime,
                                       CreatedBy = currentUserId
                                   };

                               _db.TankerBalanceLogs
                                   .Add(adjustLog);
                           }
                           else
                           {
                               decimal clearedAmount =
                                   oldBalance;

                               decimal remainingProfit =
                                   grandTotal - oldBalance;

                               model.AmountPay =
                                   clearedAmount;

                               tanker.PreviousBalance = 0;

                               newBalance = 0;

                               TankerBalanceLog clearBalanceLog =
                                   new TankerBalanceLog()
                                   {
                                       TankerId = tanker.Id,
                                       EntryDate = pakTime,
                                       TransactionType = "DEDUCT",
                                       ModuleName =
                                           "Trip Ledger Balance Clear",
                                       Amount = clearedAmount,
                                       PreviousBalance = oldBalance,
                                       NewBalance = 0,
                                       Description =
                                           "Previous tanker balance fully cleared from trip profit.",
                                       CreatedAt = pakTime,
                                       CreatedBy = currentUserId
                                   };

                               _db.TankerBalanceLogs
                                   .Add(clearBalanceLog);

                               // =========================
                               // EXTRA PROFIT LOG
                               // =========================
                               if (remainingProfit > 0)
                               {
                                   TankerBalanceLog extraProfitLog =
                                       new TankerBalanceLog()
                                       {
                                           TankerId = tanker.Id,
                                           EntryDate = pakTime,
                                           TransactionType = "ADD",
                                           ModuleName =
                                               "Trip Ledger Extra Profit",
                                           Amount = remainingProfit,
                                           PreviousBalance = 0,
                                           NewBalance = 0,
                                           Description =
                                               $"Extra profit generated after clearing tanker balance. Extra Profit: {remainingProfit}",
                                           CreatedAt = pakTime,
                                           CreatedBy = currentUserId
                                       };

                                   _db.TankerBalanceLogs
                                       .Add(extraProfitLog);
                               }
                           }
                       }
                       else
                       {
                           // =====================================
                           // NO PREVIOUS BALANCE
                           // =====================================
                           model.AmountPay =
                               grandTotal;

                           TankerBalanceLog profitLog =
                               new TankerBalanceLog()
                               {
                                   TankerId = tanker.Id,
                                   EntryDate = pakTime,
                                   TransactionType = "ADD",
                                   ModuleName =
                                       "Trip Ledger Profit",
                                   Amount = grandTotal,
                                   PreviousBalance = oldBalance,
                                   NewBalance = oldBalance,
                                   Description =
                                       $"Trip profit generated. Profit Amount: {grandTotal}",
                                   CreatedAt = pakTime,
                                   CreatedBy = currentUserId
                               };

                           _db.TankerBalanceLogs
                               .Add(profitLog);
                       }
                   }

                   // =========================================
                   // LOSS CASE
                   // =========================================
                   else if (grandTotal < 0)
                   {
                       decimal lossAmount =
                           Math.Abs(grandTotal);

                       tanker.PreviousBalance =
                           oldBalance + lossAmount;

                       newBalance =
                           tanker.PreviousBalance;

                       model.AmountPay = 0;

                       TankerBalanceLog lossLog =
                           new TankerBalanceLog()
                           {
                               TankerId = tanker.Id,
                               EntryDate = pakTime,
                               TransactionType = "ADD",
                               ModuleName =
                                   "Trip Ledger Loss",
                               Amount = lossAmount,
                               PreviousBalance = oldBalance,
                               NewBalance = newBalance,
                               Description =
                                   $"Loss added into tanker pending balance. Loss Amount: {lossAmount}",
                               CreatedAt = pakTime,
                               CreatedBy = currentUserId
                           };

                       _db.TankerBalanceLogs
                           .Add(lossLog);
                   }

                   // =========================================
                   // SAVE LEDGER FIRST
                   // =========================================
                   _db.TripLedgers.Add(model);

                   _db.SaveChanges();

                   // =========================================
                   // UPDATE REFERENCE IDs
                   // =========================================
                   var pendingLogs =
                       _db.ChangeTracker
                       .Entries<TankerBalanceLog>()
                       .Where(x =>
                           x.Entity.ReferenceId == 0)
                       .Select(x => x.Entity)
                       .ToList();

                   foreach (var log in pendingLogs)
                   {
                       log.ReferenceId = model.Id;
                   }

                   // =========================================
                   // SAVE EXPENSES
                   // =========================================
                   if (uniqueExpenses.Count > 0)
                   {
                       foreach (var item in uniqueExpenses)
                       {
                           item.TripLedgerId =
                               model.Id;

                           item.CreatedAt =
                               pakTime;

                           item.CreatedBy =
                               currentUserId;

                           _db.TripExpenses.Add(item);
                       }
                   }

                   // ==================================================
                   // CASH LEDGER ENTRIES
                   // ==================================================

                   // =========================
                   // ADVANCE CASH ENTRY
                   // =========================
                   if (model.AdvanceCash > 0)
                   {
                       currentCashBalance -=
                           model.AdvanceCash;

                       CashLedger advanceEntry =
                           new CashLedger()
                           {
                               EntryDate = pakTime,
                               Description =
                                   $"Trip Ledger Advance Cash | Token#: {model.TokenNo}",
                               Debit = model.AdvanceCash,
                               Credit = 0,
                               Balance = currentCashBalance,
                               CreatedAt = pakTime,
                               CreatedBy = currentUserId
                           };

                       _db.CashLedgers
                           .Add(advanceEntry);
                   }

                   // =========================
                   // SHORTAGE ENTRY
                   // =========================
                   if (model.Shortage > 0)
                   {
                       currentCashBalance -=
                           model.Shortage;

                       CashLedger shortageEntry =
                           new CashLedger()
                           {
                               EntryDate = pakTime,
                               Description =
                                   $"Trip Ledger Shortage | Token#: {model.TokenNo}",
                               Debit = model.Shortage,
                               Credit = 0,
                               Balance = currentCashBalance,
                               CreatedAt = pakTime,
                               CreatedBy = currentUserId
                           };

                       _db.CashLedgers
                           .Add(shortageEntry);
                   }

                   // =========================
                   // MUNSHIANA ENTRY
                   // =========================
                   if (model.Munshiana > 0)
                   {
                       currentCashBalance -=
                           model.Munshiana;

                       CashLedger munshianaEntry =
                           new CashLedger()
                           {
                               EntryDate = pakTime,
                               Description =
                                   $"Trip Ledger Munshiana | Token#: {model.TokenNo}",
                               Debit = model.Munshiana,
                               Credit = 0,
                               Balance = currentCashBalance,
                               CreatedAt = pakTime,
                               CreatedBy = currentUserId
                           };

                       _db.CashLedgers
                           .Add(munshianaEntry);
                   }

                   // =========================
                   // EXPENSE LEDGER ENTRIES
                   // =========================
                   if (uniqueExpenses.Count > 0)
                   {
                       foreach (var item in uniqueExpenses)
                       {
                           currentCashBalance -=
                               item.Amount;

                           CashLedger expenseLedger =
                               new CashLedger()
                               {
                                   EntryDate = pakTime,
                                   Description =
                                       $"Trip Expense: {item.ExpenseName} | Token#: {model.TokenNo}",
                                   Debit = item.Amount,
                                   Credit = 0,
                                   Balance = currentCashBalance,
                                   CreatedAt = pakTime,
                                   CreatedBy = currentUserId
                               };

                           _db.CashLedgers
                               .Add(expenseLedger);
                       }
                   }

                   // =========================================
                   // FINAL SAVE
                   // =========================================
                   _db.SaveChanges();

                   transaction.Commit();

                   // =========================
                   // SUCCESS
                   // =========================
                   TempData["add_ledger"] =
                       "Ledger Added Successfully";

                   return RedirectToAction("Index");
               }
               catch (Exception ex)
               {
                   transaction.Rollback();

                   TempData["Error"] =
                       ex.Message;

                   return RedirectToAction(
                       "AddLedger",
                       new
                       {
                           tripId = vm.TripEntryId
                       });
               }
           }

           // EDIT GET
           public IActionResult EditLedger(int id)
           {
               // =========================
               // FETCH LEDGER
               // =========================
               var ledger = _db.TripLedgers
                   .Include(x => x.Expenses)
                   .FirstOrDefault(x => x.Id == id);

               if (ledger == null)
               {
                   return NotFound();
               }

               // =========================
               // FETCH TRIP + TANKER
               // =========================
               var trip = _db.TripEntries
                   .Include(x => x.TankerFk)
                   .FirstOrDefault(x => x.Id == ledger.TripEntryId);

               if (trip == null)
               {
                   return NotFound();
               }

               // =========================
               // FETCH EXPENSES
               // =========================
               var expenses = _db.TripExpenses
                   .Where(x => x.TripLedgerId == ledger.Id)
                   .ToList();

               // =========================
               // MAP ENTITY TO VIEWMODEL
               // =========================
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

               // =========================
               // VIEWBAGS
               // =========================
               ViewBag.Trip = trip;

               ViewBag.Products =
                   _db.Products.ToList();

               ViewBag.Expenses = expenses;

               // OLD PAYING AMOUNT REVERSE
               ViewBag.TankerPreviousBalance =
                   trip.TankerFk.PreviousBalance
                   + (ledger.AmountPay ?? 0);

               return View(vm);
           }

           [HttpPost]
           [ValidateAntiForgeryToken]
           public IActionResult EditLedger(TripLedgerVM vm, List<TripExpense> expenses)
           {
               // =========================
               // FETCH OLD LEDGER
               // =========================
               var ledger = _db.TripLedgers
                   .FirstOrDefault(x => x.Id == vm.Id);

               if (ledger == null)
               {
                   return NotFound();
               }

               // =========================
               // TOKEN CHECK
               // =========================
               bool tokenExists = _db.TripLedgers
                   .Any(x =>
                       x.TokenNo == vm.TokenNo &&
                       x.Id != vm.Id);

               if (tokenExists)
               {
                   ModelState.AddModelError(
                       "TokenNo",
                       "Token No already exists."
                   );

                   ViewBag.Products = _db.Products.ToList();

                   return View(vm);
               }

               // =========================
               // FETCH TRIP + TANKER
               // =========================
               var trip = _db.TripEntries
                   .Include(x => x.TankerFk)
                   .FirstOrDefault(x => x.Id == ledger.TripEntryId);

               if (trip == null)
               {
                   return NotFound();
               }

               var tanker = trip.TankerFk;

               // =========================
               // PAKISTAN TIME
               // =========================
               var tz = TimeZoneInfo.FindSystemTimeZoneById(
                   "Pakistan Standard Time");

               DateTime pakTime =
                   TimeZoneInfo.ConvertTimeFromUtc(
                       DateTime.UtcNow,
                       tz);

               // =========================
               // SESSION
               // =========================
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

               // =========================
               // REMOVE OLD EFFECT
               // =========================

               decimal oldBalance =
                   tanker.PreviousBalance;

               // =========================
               // REVERSE OLD BALANCE EFFECT
               // =========================

               if (ledger.GrandTotal > 0)
               {
                   // =========================
                   // PREVIOUS PROFIT CASE
                   // =========================

                   tanker.PreviousBalance =
                       tanker.PreviousBalance + (ledger.AmountPay ?? 0);
               }
               else if (ledger.GrandTotal < 0)
               {
                   // =========================
                   // PREVIOUS LOSS CASE
                   // =========================

                   decimal oldLoss =
                       Math.Abs(ledger.GrandTotal);

                   tanker.PreviousBalance =
                       tanker.PreviousBalance - oldLoss;
               }

               // =========================
               // CURRENT BALANCE
               // =========================
               decimal currentBalance =
                   tanker.PreviousBalance;

               // =========================
               // UPDATE LEDGER FIELDS
               // =========================
               ledger.Freight =
                   vm.Freight;

               ledger.AdvanceCash =
                   vm.AdvanceCash;

               ledger.Shortage =
                   vm.Shortage;

               ledger.Munshiana =
                   vm.Munshiana;

               ledger.TokenNo =
                   vm.TokenNo;

               ledger.ProductId =
                   vm.ProductId;

               ledger.TripDate =
                   vm.TripDate;

               ledger.Remarks =
                   string.IsNullOrWhiteSpace(vm.Remarks)
                   ? "N/A"
                   : vm.Remarks;

               // =========================
               // COMMISSION
               // =========================
               var commissionSetup =
                   _db.CommissionSetups
                   .FirstOrDefault(x => x.IsActive);

               decimal commissionPercent =
                   commissionSetup?.Percentage ?? 0;

               ledger.Commission =
                   (ledger.Freight * commissionPercent) / 100;

               // =========================
               // CLEAN EXPENSES
               // =========================
               if (expenses == null)
               {
                   expenses = new List<TripExpense>();
               }

               var uniqueExpenses = expenses
                   .Where(x =>
                       !string.IsNullOrWhiteSpace(x.ExpenseName)
                       && x.Amount > 0
                   )
                   .GroupBy(x => new
                   {
                       ExpenseName = x.ExpenseName
                           .Trim()
                           .ToLower(),

                       x.Amount
                   })
                   .Select(g => g.First())
                   .ToList();

               // =========================
               // REMOVE OLD EXPENSES
               // =========================
               var oldExpenses =
                   _db.TripExpenses
                   .Where(x => x.TripLedgerId == ledger.Id)
                   .ToList();

               _db.TripExpenses.RemoveRange(oldExpenses);

               // =========================
               // ADD NEW EXPENSES
               // =========================
               decimal totalExpense =
                   uniqueExpenses.Sum(x => x.Amount);

               foreach (var item in uniqueExpenses)
               {
                   item.TripLedgerId =
                       ledger.Id;

                   item.CreatedAt =
                       pakTime;

                   item.CreatedBy =
                       currentUserId;

                   _db.TripExpenses.Add(item);
               }

               // =========================
               // TOTAL DEDUCTIONS
               // =========================
               decimal totalDeductions =
                   ledger.AdvanceCash
                   + ledger.Shortage
                   + ledger.Commission
                   + ledger.Munshiana
                   + totalExpense;

               ledger.TotalDeductions =
                   totalDeductions;

               // =========================
               // GRAND TOTAL
               // =========================
               decimal grandTotal =
                   ledger.Freight - totalDeductions;

               ledger.GrandTotal =
                   grandTotal;

               // =========================
               // RESET
               // =========================
               ledger.AmountPay = 0;

               decimal newBalance =
                   currentBalance;

               // =========================================
               // PROFIT CASE
               // =========================================
               if (grandTotal > 0)
               {
                   // =====================================
                   // IF PREVIOUS BALANCE EXISTS
                   // =====================================
                   if (currentBalance > 0)
                   {
                       // =====================================
                       // PROFIT <= BALANCE
                       // =====================================
                       if (grandTotal <= currentBalance)
                       {
                           ledger.AmountPay =
                               grandTotal;

                           tanker.PreviousBalance =
                               currentBalance - grandTotal;

                           newBalance =
                               tanker.PreviousBalance;

                           TankerBalanceLog adjustLog =
                               new TankerBalanceLog()
                               {
                                   TankerId = tanker.Id,

                                   ReferenceId = ledger.Id,

                                   EntryDate = pakTime,

                                   TransactionType = "DEDUCT",

                                   ModuleName = "Edit Ledger Profit Adjustment",

                                   Amount = grandTotal,

                                   PreviousBalance = currentBalance,

                                   NewBalance = newBalance,

                                   Description =
                                       $"Edited ledger profit adjusted against tanker balance. Paid Amount: {grandTotal}",

                                   CreatedAt = pakTime,

                                   CreatedBy = currentUserId
                               };

                           _db.TankerBalanceLogs.Add(adjustLog);
                       }
                       else
                       {
                           // =====================================
                           // PROFIT > BALANCE
                           // =====================================

                           decimal clearedAmount =
                               currentBalance;

                           decimal remainingProfit =
                               grandTotal - currentBalance;

                           ledger.AmountPay =
                               clearedAmount;

                           tanker.PreviousBalance = 0;

                           newBalance = 0;

                           TankerBalanceLog clearBalanceLog =
                               new TankerBalanceLog()
                               {
                                   TankerId = tanker.Id,

                                   ReferenceId = ledger.Id,

                                   EntryDate = pakTime,

                                   TransactionType = "DEDUCT",

                                   ModuleName = "Edit Ledger Balance Clear",

                                   Amount = clearedAmount,

                                   PreviousBalance = currentBalance,

                                   NewBalance = 0,

                                   Description =
                                       $"Edited ledger cleared tanker balance from profit.",

                                   CreatedAt = pakTime,

                                   CreatedBy = currentUserId
                               };

                           _db.TankerBalanceLogs.Add(clearBalanceLog);

                           if (remainingProfit > 0)
                           {
                               TankerBalanceLog extraProfitLog =
                                   new TankerBalanceLog()
                                   {
                                       TankerId = tanker.Id,

                                       ReferenceId = ledger.Id,

                                       EntryDate = pakTime,

                                       TransactionType = "ADD",

                                       ModuleName = "Edit Ledger Extra Profit",

                                       Amount = remainingProfit,

                                       PreviousBalance = 0,

                                       NewBalance = 0,

                                       Description =
                                           $"Edited ledger generated extra profit. Extra Profit: {remainingProfit}",

                                       CreatedAt = pakTime,

                                       CreatedBy = currentUserId
                                   };

                               _db.TankerBalanceLogs.Add(extraProfitLog);
                           }
                       }
                   }
                   else
                   {
                       // =====================================
                       // NO PREVIOUS BALANCE
                       // =====================================

                       ledger.AmountPay =
                           grandTotal;

                       TankerBalanceLog profitLog =
                           new TankerBalanceLog()
                           {
                               TankerId = tanker.Id,

                               ReferenceId = ledger.Id,

                               EntryDate = pakTime,

                               TransactionType = "ADD",

                               ModuleName = "Edit Ledger Profit",

                               Amount = grandTotal,

                               PreviousBalance = currentBalance,

                               NewBalance = currentBalance,

                               Description =
                                   $"Edited ledger generated profit. Profit Amount: {grandTotal}",

                               CreatedAt = pakTime,

                               CreatedBy = currentUserId
                           };

                       _db.TankerBalanceLogs.Add(profitLog);
                   }
               }

               // =========================================
               // LOSS CASE
               // =========================================
               else if (grandTotal < 0)
               {
                   decimal lossAmount =
                       Math.Abs(grandTotal);

                   tanker.PreviousBalance =
                       currentBalance + lossAmount;

                   newBalance =
                       tanker.PreviousBalance;

                   ledger.AmountPay = 0;

                   TankerBalanceLog lossLog =
                       new TankerBalanceLog()
                       {
                           TankerId = tanker.Id,

                           ReferenceId = ledger.Id,

                           EntryDate = pakTime,

                           TransactionType = "ADD",

                           ModuleName = "Edit Ledger Loss",

                           Amount = lossAmount,

                           PreviousBalance = currentBalance,

                           NewBalance = newBalance,

                           Description =
                               $"Edited ledger added loss into tanker balance. Loss Amount: {lossAmount}",

                           CreatedAt = pakTime,

                           CreatedBy = currentUserId
                       };

                   _db.TankerBalanceLogs.Add(lossLog);
               }

               // =========================
               // UPDATED INFO
               // =========================
               ledger.UpdatedAt =
                   pakTime;

               ledger.UpdatedBy =
                   currentUserId;

               // =========================
               // SAVE
               // =========================
               _db.SaveChanges();

               TempData["edit_ledger"] =
                   "Ledger Updated Successfully";

               return RedirectToAction("Index");
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
     */

        // LIST
        /*   public IActionResult Index()
           {
               var data = _db.TripLedgers
                   .Include(x => x.Product)
                   .Include(x => x.TripEntryFk)
                   .OrderByDescending(x => x.Id)
                   .ToList();

               return View(data);
           }
           // ADD GET
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
               ViewBag.TankerPreviousBalance = trip.TankerFk?.PreviousBalance ?? 0;

               return View();
           }


           // Update Code 
           [HttpPost]
           [ValidateAntiForgeryToken]
           public IActionResult AddLedger(TripLedgerVM vm, List<TripExpense> expenses)
           {
               using var transaction = _db.Database.BeginTransaction();

               try
               {
                   // =========================
                   // CREATE ENTITY
                   // =========================
                   var model = new TripLedger()
                   {
                       TripDate = vm.TripDate,
                       TripEntryId = vm.TripEntryId,
                       ProductId = vm.ProductId,
                       Freight = vm.Freight,
                       AdvanceCash = vm.AdvanceCash,
                       Shortage = vm.Shortage,
                       Commission = vm.Commission,
                       Munshiana = vm.Munshiana,
                       TokenNo = vm.TokenNo,
                       Remarks = vm.Remarks
                   };

                   // =========================
                   // PAKISTAN TIME
                   // =========================
                   var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                   DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                   model.CreatedAt = pakTime;

                   // =========================
                   // SESSION CHECK
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

                   model.CreatedBy = currentUserId;

                   // =========================
                   // TOKEN CHECK
                   // =========================
                   bool tokenExists = _db.TripLedgers.Any(x => x.TokenNo == model.TokenNo);
                   if (tokenExists)
                   {
                       ModelState.AddModelError("TokenNo", "Token No already exists.");
                       ViewBag.Products = _db.Products.ToList();
                       return View(vm);
                   }

                   // =========================
                   // FETCH TRIP + TANKER
                   // =========================
                   var trip = _db.TripEntries
                       .Include(x => x.TankerFk)
                       .FirstOrDefault(x => x.Id == model.TripEntryId);

                   if (trip == null) return NotFound();

                   var tanker = trip.TankerFk;

                   // =========================================================
                   // UPDATED: Mapping to new CurrentBalance architecture
                   // =========================================================
                   ViewBag.Trip = trip;
                   ViewBag.Products = _db.Products.ToList();
                   ViewBag.TankerPreviousBalance = tanker?.CurrentBalance ?? 0;
                   decimal oldBalance = tanker?.CurrentBalance ?? 0;

                   if (string.IsNullOrWhiteSpace(model.Remarks))
                   {
                       model.Remarks = "N/A";
                   }

                   // =========================
                   // COMMISSION % & CALCULATE
                   // =========================
                   var commissionSetup = _db.CommissionSetups.FirstOrDefault(x => x.IsActive);
                   decimal commissionPercent = commissionSetup?.Percentage ?? 0;
                   model.Commission = (model.Freight * commissionPercent) / 100;

                   // =========================
                   // CLEAN EXPENSES & TOTALS
                   // =========================
                   if (expenses == null) expenses = new List<TripExpense>();

                   var uniqueExpenses = expenses
                       .Where(x => !string.IsNullOrWhiteSpace(x.ExpenseName) && x.Amount > 0)
                       .GroupBy(x => new { ExpenseName = x.ExpenseName.Trim().ToLower(), x.Amount })
                       .Select(g => g.First())
                       .ToList();

                   decimal totalExpense = uniqueExpenses.Sum(x => x.Amount);

                   decimal totalCashRequired = model.AdvanceCash + model.Shortage + model.Munshiana + totalExpense;

                   // =========================
                   // CASH LEDGER CHECK
                   // =========================
                   var cashLedger = _db.CashLedgers.OrderByDescending(x => x.Id).FirstOrDefault();
                   decimal currentCashBalance = cashLedger?.Balance ?? 0;

                   if (currentCashBalance < totalCashRequired)
                   {
                       TempData["Error"] = $"Cash Ledger balance is low. Available Balance: {currentCashBalance}";
                       return RedirectToAction("AddLedger", new { tripId = vm.TripEntryId });
                   }

                   // =========================
                   // CALCULATE DEDUCTIONS & GRAND TOTAL
                   // =========================
                   decimal totalDeductions = model.AdvanceCash + model.Shortage + model.Commission + model.Munshiana + totalExpense;
                   model.TotalDeductions = totalDeductions;

                   decimal grandTotal = model.Freight - totalDeductions;
                   model.GrandTotal = grandTotal;

                   // Under single-sign logic: Net Running Balance changes mathematically by adding the GrandTotal
                   decimal newBalance = oldBalance + grandTotal;
                   model.AmountPay = grandTotal; // Amount generated/deducted during this trip

                   if (tanker != null)
                   {
                       tanker.CurrentBalance = newBalance;
                   }

                   // =========================================================
                   // NEW: Standardized Double-Entry Accounting Log
                   // =========================================================
                   if (grandTotal != 0 && tanker != null)
                   {
                       decimal creditAmount = grandTotal > 0 ? grandTotal : 0;
                       decimal debitAmount = grandTotal < 0 ? Math.Abs(grandTotal) : 0;

                       string logDescription = grandTotal > 0
                           ? $"Trip Profit Credited. Token#: {model.TokenNo}, Freight: {model.Freight}"
                           : $"Trip Loss Debited. Token#: {model.TokenNo}, Total Deductions exceeded Freight.";

                       TankerLedger ledgerLog = new TankerLedger()
                       {
                           TankerId = tanker.Id,
                           TransactionDate = pakTime,
                           ModuleName = "Trip Ledger Add",
                           ReferenceId = 0, // Will be updated below after model saves and gets an ID
                           Credit = creditAmount,
                           Debit = debitAmount,
                           RunningBalance = newBalance,
                           Description = logDescription,
                           CreatedAt = pakTime,
                           CreatedBy = currentUserId
                       };

                       _db.TankerLedgers.Add(ledgerLog);
                   }

                   // =========================================
                   // SAVE LEDGER FIRST
                   // =========================================
                   _db.TripLedgers.Add(model);
                   _db.SaveChanges();

                   // =========================================
                   // UPDATE REFERENCE IDs FOR TANKER LEDGER
                   // =========================================
                   var pendingLogs = _db.ChangeTracker
                       .Entries<TankerLedger>()
                       .Where(x => x.Entity.ReferenceId == 0)
                       .Select(x => x.Entity)
                       .ToList();

                   foreach (var log in pendingLogs)
                   {
                       log.ReferenceId = model.Id;
                   }

                   // =========================================
                   // SAVE EXPENSES
                   // =========================================
                   if (uniqueExpenses.Count > 0)
                   {
                       foreach (var item in uniqueExpenses)
                       {
                           item.TripLedgerId = model.Id;
                           item.CreatedAt = pakTime;
                           item.CreatedBy = currentUserId;
                           _db.TripExpenses.Add(item);
                       }
                   }

                   // ==================================================
                   // CASH LEDGER ENTRIES
                   // ==================================================
                   if (model.AdvanceCash > 0)
                   {
                       currentCashBalance -= model.AdvanceCash;
                       _db.CashLedgers.Add(new CashLedger()
                       {
                           EntryDate = pakTime,
                           Description = $"Trip Ledger Advance Cash | Token#: {model.TokenNo}",
                           Debit = model.AdvanceCash,
                           Credit = 0,
                           Balance = currentCashBalance,
                           CreatedAt = pakTime,
                           CreatedBy = currentUserId
                       });
                   }

                   if (model.Shortage > 0)
                   {
                       currentCashBalance -= model.Shortage;
                       _db.CashLedgers.Add(new CashLedger()
                       {
                           EntryDate = pakTime,
                           Description = $"Trip Ledger Shortage | Token#: {model.TokenNo}",
                           Debit = model.Shortage,
                           Credit = 0,
                           Balance = currentCashBalance,
                           CreatedAt = pakTime,
                           CreatedBy = currentUserId
                       });
                   }

                   if (model.Munshiana > 0)
                   {
                       currentCashBalance -= model.Munshiana;
                       _db.CashLedgers.Add(new CashLedger()
                       {
                           EntryDate = pakTime,
                           Description = $"Trip Ledger Munshiana | Token#: {model.TokenNo}",
                           Debit = model.Munshiana,
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
                               Description = $"Trip Expense: {item.ExpenseName} | Token#: {model.TokenNo}",
                               Debit = item.Amount,
                               Credit = 0,
                               Balance = currentCashBalance,
                               CreatedAt = pakTime,
                               CreatedBy = currentUserId
                           });
                       }
                   }

                   // Final Save & Commit
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

           // EDIT GET
           public IActionResult EditLedger(int id)
           {
               // =========================
               // FETCH LEDGER
               // =========================
               var ledger = _db.TripLedgers
                   .Include(x => x.Expenses)
                   .FirstOrDefault(x => x.Id == id);

               if (ledger == null) return NotFound();

               // =========================
               // FETCH TRIP + TANKER
               // =========================
               var trip = _db.TripEntries
                   .Include(x => x.TankerFk)
                   .FirstOrDefault(x => x.Id == ledger.TripEntryId);

               if (trip == null) return NotFound();

               // =========================
               // FETCH EXPENSES
               // =========================
               var expenses = _db.TripExpenses
                   .Where(x => x.TripLedgerId == ledger.Id)
                   .ToList();

               // =========================
               // MAP ENTITY TO VIEWMODEL
               // =========================
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

               // =========================
               // VIEWBAGS
               // =========================
               ViewBag.Trip = trip;
               ViewBag.Products = _db.Products.ToList();
               ViewBag.Expenses = expenses;

               // =========================================================================
               // UPDATED: Reverse calculation for previewing previous state
               // Formula: Current Balance minus the effect of this trip's net amount pay
               // =========================================================================
               ViewBag.TankerPreviousBalance = (trip.TankerFk?.CurrentBalance ?? 0) - (ledger.AmountPay ?? 0);

               return View(vm);
           }
   */

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

        // ==========================================
        // ADD LEDGER (POST)
        // ==========================================
        /* [HttpPost]
         [ValidateAntiForgeryToken]
         public IActionResult AddLedger(TripLedgerVM vm, List<TripExpense> expenses)
         {
             using var transaction = _db.Database.BeginTransaction();

             try
             {
                 // 1. Session Check First (Kyunki agar session out hai toh baki mehnat fazool hai)
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
                     return View(vm);
                 }

                 // 3. Fetch Trip & Tanker
                 var trip = _db.TripEntries
                     .Include(x => x.TankerFk)
                     .FirstOrDefault(x => x.Id == vm.TripEntryId);

                 if (trip == null) return NotFound();
                 var tanker = trip.TankerFk;

                 // 4. Time Setup
                 var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                 DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                 // 5. Calculations
                 var commissionSetup = _db.CommissionSetups.FirstOrDefault(x => x.IsActive);
                 decimal commissionPercent = commissionSetup?.Percentage ?? 0;
                 decimal calculatedCommission = (vm.Freight * commissionPercent) / 100;

                 if (expenses == null) expenses = new List<TripExpense>();
                 var uniqueExpenses = expenses
                     .Where(x => !string.IsNullOrWhiteSpace(x.ExpenseName) && x.Amount > 0)
                     .GroupBy(x => new { ExpenseName = x.ExpenseName.Trim().ToLower(), x.Amount })
                     .Select(g => g.First())
                     .ToList();

                 decimal totalExpense = uniqueExpenses.Sum(x => x.Amount);
                 decimal totalCashRequired = vm.AdvanceCash + vm.Shortage + vm.Munshiana + totalExpense;

                 // 6. Cash Balance Validation
                 var cashLedger = _db.CashLedgers.OrderByDescending(x => x.Id).FirstOrDefault();
                 decimal currentCashBalance = cashLedger?.Balance ?? 0;

                 if (currentCashBalance < totalCashRequired)
                 {
                     TempData["Error"] = $"Cash Ledger balance is low. Available Balance: {currentCashBalance}";
                     return RedirectToAction("AddLedger", new { tripId = vm.TripEntryId });
                 }

                 decimal totalDeductions = vm.AdvanceCash + vm.Shortage + calculatedCommission + vm.Munshiana + totalExpense;
                 decimal grandTotal = vm.Freight - totalDeductions;

                 // 7. Map & Save Trip Ledger Entity First (To generate Model.Id)
                 var model = new TripLedger()
                 {
                     TripDate = vm.TripDate,
                     TripEntryId = vm.TripEntryId,
                     ProductId = vm.ProductId,
                     Freight = vm.Freight,
                     AdvanceCash = vm.AdvanceCash,
                     Shortage = vm.Shortage,
                     Commission = calculatedCommission,
                     Munshiana = vm.Munshiana,
                     TokenNo = vm.TokenNo,
                     Remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? "N/A" : vm.Remarks,
                     TotalDeductions = totalDeductions,
                     GrandTotal = grandTotal,
                     AmountPay = grandTotal,
                     CreatedAt = pakTime,
                     CreatedBy = currentUserId
                 };

                 _db.TripLedgers.Add(model);
                 _db.SaveChanges(); // Id generated here!

                 // 8. Update Tanker Balance & Log Double-Entry Ledger
                 if (tanker != null)
                 {
                     decimal oldBalance = tanker.CurrentBalance;
                     decimal newBalance = oldBalance + grandTotal;
                     tanker.CurrentBalance = newBalance;

                     if (grandTotal != 0)
                     {
                         decimal creditAmount = grandTotal > 0 ? grandTotal : 0;
                         decimal debitAmount = grandTotal < 0 ? Math.Abs(grandTotal) : 0;

                         string logDescription = grandTotal > 0
                             ? $"Trip Profit Credited. Token#: {model.TokenNo}, Freight: {model.Freight}"
                             : $"Trip Loss Debited. Token#: {model.TokenNo}, Deductions exceeded Freight.";

                         TankerLedger ledgerLog = new TankerLedger()
                         {
                             TankerId = tanker.Id,
                             TransactionDate = pakTime,
                             ModuleName = "Trip Ledger Add",
                             ReferenceId = model.Id, // Link mapping fixed direct here
                             Credit = creditAmount,
                             Debit = debitAmount,
                             RunningBalance = newBalance,
                             Description = logDescription,
                             CreatedAt = pakTime,
                             CreatedBy = currentUserId
                         };

                         _db.TankerLedgers.Add(ledgerLog);
                     }
                 }

                 // 9. Save Expenses
                 if (uniqueExpenses.Count > 0)
                 {
                     foreach (var item in uniqueExpenses)
                     {
                         item.TripLedgerId = model.Id;
                         item.CreatedAt = pakTime;
                         item.CreatedBy = currentUserId;
                         _db.TripExpenses.Add(item);
                     }
                 }

                 // 10. Cash Ledger Log Entries
                 if (model.AdvanceCash > 0)
                 {
                     currentCashBalance -= model.AdvanceCash;
                     _db.CashLedgers.Add(new CashLedger()
                     {
                         EntryDate = pakTime,
                         Description = $"Trip Ledger Advance Cash | Token#: {model.TokenNo}",
                         Debit = model.AdvanceCash,
                         Credit = 0,
                         Balance = currentCashBalance,
                         CreatedAt = pakTime,
                         CreatedBy = currentUserId
                     });
                 }

                 if (model.Shortage > 0)
                 {
                     currentCashBalance -= model.Shortage;
                     _db.CashLedgers.Add(new CashLedger()
                     {
                         EntryDate = pakTime,
                         Description = $"Trip Ledger Shortage | Token#: {model.TokenNo}",
                         Debit = model.Shortage,
                         Credit = 0,
                         Balance = currentCashBalance,
                         CreatedAt = pakTime,
                         CreatedBy = currentUserId
                     });
                 }

                *//* if (model.Munshiana > 0)
                 {
                     currentCashBalance -= model.Munshiana;
                     _db.CashLedgers.Add(new CashLedger()
                     {
                         EntryDate = pakTime,
                         Description = $"Trip Ledger Munshiana | Token#: {model.TokenNo}",
                         Debit = model.Munshiana,
                         Credit = 0,
                         Balance = currentCashBalance,
                         CreatedAt = pakTime,
                         CreatedBy = currentUserId
                     });
                 }*//*

                 if (uniqueExpenses.Count > 0)
                 {
                     foreach (var item in uniqueExpenses)
                     {
                         currentCashBalance -= item.Amount;
                         _db.CashLedgers.Add(new CashLedger()
                         {
                             EntryDate = pakTime,
                             Description = $"Trip Expense: {item.ExpenseName} | Token#: {model.TokenNo}",
                             Debit = item.Amount,
                             Credit = 0,
                             Balance = currentCashBalance,
                             CreatedAt = pakTime,
                             CreatedBy = currentUserId
                         });
                     }
                 }

                 // Final Save Changes for all related logs & commit
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
 */
        // Working

        /*  [HttpPost]
          [ValidateAntiForgeryToken]
          public IActionResult AddLedger(TripLedgerVM vm, List<TripExpense> expenses)
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

                      // Re-fetch trip data to prevent page crashes on validation reload
                      var originalTrip = _db.TripEntries.Include(x => x.TankerFk).FirstOrDefault(x => x.Id == vm.TripEntryId);
                      if (originalTrip != null)
                      {
                          ViewBag.Trip = originalTrip;
                          ViewBag.TankerPreviousBalance = originalTrip.TankerFk?.CurrentBalance ?? 0;
                      }
                      return View(vm);
                  }

                  // 3. Fetch Trip & Tanker
                  var trip = _db.TripEntries
                      .Include(x => x.TankerFk)
                      .FirstOrDefault(x => x.Id == vm.TripEntryId);

                  if (trip == null) return NotFound();
                  var tanker = trip.TankerFk;

                  // 4. Time Setup
                  var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                  DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                  // 5. Automatic Commission Fetching & Calculations
                  // Input dependent dynamic configuration backup setup
                  var commissionSetup = _db.CommissionSetups.FirstOrDefault(x => x.IsActive);
                  decimal commissionPercent = commissionSetup?.Percentage ?? 0;
                  decimal calculatedCommission = (vm.Freight * commissionPercent) / 100;

                  if (expenses == null) expenses = new List<TripExpense>();
                  var uniqueExpenses = expenses
                      .Where(x => !string.IsNullOrWhiteSpace(x.ExpenseName) && x.Amount > 0)
                      .GroupBy(x => new { ExpenseName = x.ExpenseName.Trim().ToLower(), x.Amount })
                      .Select(g => g.First())
                      .ToList();

                  decimal totalExpense = uniqueExpenses.Sum(x => x.Amount);

                  // 🔥 REQUIREMENT: Shortage aur Commission ab Cash Ledger ko hit nahi karega.
                  // Isliye validation check me sirf actual cash transactions (Advance + Expenses + Munshiana) check honge.
                  decimal totalCashRequired = vm.AdvanceCash + vm.Munshiana + totalExpense;

                  // 6. Cash Balance Validation
                  var cashLedger = _db.CashLedgers.OrderByDescending(x => x.Id).FirstOrDefault();
                  decimal currentCashBalance = cashLedger?.Balance ?? 0;

                  if (currentCashBalance < totalCashRequired)
                  {
                      TempData["Error"] = $"Cash Ledger balance is low. Available Cash Required: {totalCashRequired}. Balance: {currentCashBalance}";
                      return RedirectToAction("AddLedger", new { tripId = vm.TripEntryId });
                  }

                  // Deductions calculation using backend generated commission target
                  decimal totalDeductions = vm.AdvanceCash + vm.Shortage + calculatedCommission + vm.Munshiana + totalExpense;
                  decimal grandTotal = vm.Freight - totalDeductions;


                  // 7. Map & Save Trip Ledger Entity First
                  var model = new TripLedger()
                  {
                      TripDate = vm.TripDate,
                      TripEntryId = vm.TripEntryId,
                      ProductId = vm.ProductId,
                      Freight = vm.Freight,
                      AdvanceCash = vm.AdvanceCash,
                      Shortage = vm.Shortage,
                      Commission = calculatedCommission, // Auto saved direct from database configurations
                      Munshiana = vm.Munshiana,
                      TokenNo = vm.TokenNo,
                      Remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? "N/A" : vm.Remarks,
                      TotalDeductions = totalDeductions,
                      GrandTotal = grandTotal,
                      AmountPay = grandTotal,
                      CreatedAt = pakTime,
                      CreatedBy = currentUserId
                  };

                  _db.TripLedgers.Add(model);
                  _db.SaveChanges(); // Generates Model.Id

                  // 8. Update Tanker Balance & Log Intelligent Ledger Entries
                  if (tanker != null)
                  {
                      decimal oldBalance = tanker.CurrentBalance;
                      decimal newBalance = oldBalance + grandTotal;
                      tanker.CurrentBalance = newBalance;

                      if (grandTotal != 0)
                      {
                          decimal creditAmount = grandTotal > 0 ? grandTotal : 0;
                          decimal debitAmount = grandTotal < 0 ? Math.Abs(grandTotal) : 0;

                          // 🔥 REQUIREMENT: Intelligent Dynamic Description generation according to outstanding dues profile
                          string logDescription = "";
                          if (grandTotal > 0)
                          {
                              if (oldBalance < 0) // Tanker was in dues liability matrix
                              {
                                  if (newBalance >= 0)
                                  {
                                      logDescription = $"Trip Profit applied: Previous outstanding dues of {Math.Abs(oldBalance)} completely cleared. Remaining profit moved to active balance. Token#: {model.TokenNo}";
                                  }
                                  else
                                  {
                                      logDescription = $"Trip Profit applied: Dues partially reduced from {Math.Abs(oldBalance)} to {Math.Abs(newBalance)}. Token#: {model.TokenNo}";
                                  }
                              }
                              else // Tanker already in standard profit or clear matrix
                              {
                                  logDescription = $"Trip Profit Credited directly to active balance. Token#: {model.TokenNo}, Freight: {model.Freight}";
                              }
                          }
                          else
                          {
                              logDescription = $"Trip Loss Debited from balance accounts. Deductions exceeded active Freight values. Token#: {model.TokenNo}";
                          }

                          TankerLedger ledgerLog = new TankerLedger()
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
                          };

                          _db.TankerLedgers.Add(ledgerLog);
                      }
                  }

                  // 9. Save Expenses
                  if (uniqueExpenses.Count > 0)
                  {
                      foreach (var item in uniqueExpenses)
                      {
                          item.TripLedgerId = model.Id;
                          item.CreatedAt = pakTime;
                          item.CreatedBy = currentUserId;
                          _db.TripExpenses.Add(item);
                      }
                  }

                  // 10. Cash Ledger Log Entries (Filter Targets Updated)
                  // Only Advance Cash, Munshiana, and Dynamic Expenses will record transactions
                  if (model.AdvanceCash > 0)
                  {
                      currentCashBalance -= model.AdvanceCash;
                      _db.CashLedgers.Add(new CashLedger()
                      {
                          EntryDate = pakTime,
                          Description = $"Trip Ledger Advance Cash Payment | Token#: {model.TokenNo}",
                          Debit = model.AdvanceCash,
                          Credit = 0,
                          Balance = currentCashBalance,
                          CreatedAt = pakTime,
                          CreatedBy = currentUserId
                      });
                  }

                  // 🔥 REQUIREMENT REMOVAL: Commission and Shortage blocks are completely omitted from hitting Cash Ledger.

                *//*  if (model.Munshiana > 0)
                  {
                      currentCashBalance -= model.Munshiana;
                      _db.CashLedgers.Add(new CashLedger()
                      {
                          EntryDate = pakTime,
                          Description = $"Trip Ledger Munshiana | Token#: {model.TokenNo}",
                          Debit = model.Munshiana,
                          Credit = 0,
                          Balance = currentCashBalance,
                          CreatedAt = pakTime,
                          CreatedBy = currentUserId
                      });
                  }
  *//*
                  if (uniqueExpenses.Count > 0)
                  {
                      foreach (var item in uniqueExpenses)
                      {
                          currentCashBalance -= item.Amount;
                          _db.CashLedgers.Add(new CashLedger()
                          {
                              EntryDate = pakTime,
                              Description = $"Trip Expense Log: {item.ExpenseName} | Token#: {model.TokenNo}",
                              Debit = item.Amount,
                              Credit = 0,
                              Balance = currentCashBalance,
                              CreatedAt = pakTime,
                              CreatedBy = currentUserId
                          });
                      }
                  }

                  // Final Save Changes for all structural updates and commit
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
        */
        //Working Start
        /*  [HttpPost]
          [ValidateAntiForgeryToken]
          public IActionResult AddLedger(TripLedgerVM vm, List<TripExpense> expenses)
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

                  // 3. Fetch Trip & Tanker
                  var trip = _db.TripEntries
                      .Include(x => x.TankerFk)
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

                  if (expenses == null) expenses = new List<TripExpense>();
                  var uniqueExpenses = expenses
                      .Where(x => !string.IsNullOrWhiteSpace(x.ExpenseName) && x.Amount > 0)
                      .GroupBy(x => new { ExpenseName = x.ExpenseName.Trim().ToLower(), x.Amount })
                      .Select(g => g.First())
                      .ToList();

                  decimal totalExpense = uniqueExpenses.Sum(x => x.Amount);

                  // 🔥 Cash required validation tracker (Excluding Shortage & Commission)
                  decimal totalCashRequired = vm.AdvanceCash + vm.Munshiana + totalExpense;

                  // 6. Cash Balance Validation
                  var cashLedger = _db.CashLedgers.OrderByDescending(x => x.Id).FirstOrDefault();
                  decimal currentCashBalance = cashLedger?.Balance ?? 0;

                  if (currentCashBalance < totalCashRequired)
                  {
                      TempData["Error"] = $"Cash Ledger balance is low. Available Cash Required: {totalCashRequired}. Balance: {currentCashBalance}";
                      return RedirectToAction("AddLedger", new { tripId = vm.TripEntryId });
                  }

                  // Calculate Grand Total (Trip Profit / Loss)
                  decimal totalDeductions = vm.AdvanceCash + vm.Shortage + calculatedCommission + vm.Munshiana + totalExpense;
                  decimal grandTotal = vm.Freight - totalDeductions;

                  // =========================================================================
                  // 🔥 CRITICAL ACCOUNTING FIX: AmountPay Calculation (Dues Clearance Tracker)
                  // =========================================================================
                  decimal calculatedAmountPay = 0;
                  decimal oldBalance = tanker?.CurrentBalance ?? 0;

                  // Agar tanker par pehle se udhaar (Dues/Negative balance) tha aur is trip par profit hua hai
                  if (oldBalance < 0 && grandTotal > 0)
                  {
                      // mathematical conversion ke liye Abs use kiya (e.g., -100,000 becomes 100,000)
                      decimal outstandingDues = Math.Abs(oldBalance);

                      // Agar profit outstanding dues se kam ya barabar hai, to poora ka poora profit dues adjust karne me laga
                      // Agar profit dues se zyada hai, to sirf utna profit use hua jitne dues the.
                      calculatedAmountPay = Math.Min(outstandingDues, grandTotal);
                  }

                  // 7. Map & Save Trip Ledger Entity First
                  var model = new TripLedger()
                  {
                      TripDate = vm.TripDate,
                      TripEntryId = vm.TripEntryId,
                      ProductId = vm.ProductId,
                      Freight = vm.Freight,
                      AdvanceCash = vm.AdvanceCash,
                      Shortage = vm.Shortage,
                      Commission = calculatedCommission,
                      Munshiana = vm.Munshiana,
                      TokenNo = vm.TokenNo,
                      Remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? "N/A" : vm.Remarks,
                      TotalDeductions = totalDeductions,
                      GrandTotal = grandTotal,
                      AmountPay = calculatedAmountPay, // 🔥 Assigned mathematically calculated dues payment trace
                      CreatedAt = pakTime,
                      CreatedBy = currentUserId
                  };

                  _db.TripLedgers.Add(model);
                  _db.SaveChanges(); // Generates model.Id

                  // 8. Update Tanker Balance & Log Intelligent Ledger Entries
                  if (tanker != null)
                  {
                      decimal newBalance = oldBalance + grandTotal;
                      tanker.CurrentBalance = newBalance;

                      if (grandTotal != 0)
                      {
                          decimal creditAmount = grandTotal > 0 ? grandTotal : 0;
                          decimal debitAmount = grandTotal < 0 ? Math.Abs(grandTotal) : 0;

                          string logDescription = "";
                          if (grandTotal > 0)
                          {
                              if (oldBalance < 0)
                              {
                                  if (newBalance >= 0)
                                  {
                                      logDescription = $"Trip Profit applied: Previous outstanding dues of {Math.Abs(oldBalance)} completely cleared. Remaining profit moved to active balance. Token#: {model.TokenNo}";
                                  }
                                  else
                                  {
                                      logDescription = $"Trip Profit applied: Dues partially reduced from {Math.Abs(oldBalance)} to {Math.Abs(newBalance)}. Token#: {model.TokenNo}";
                                  }
                              }
                              else
                              {
                                  logDescription = $"Trip Profit Credited directly to active balance. Token#: {model.TokenNo}, Freight: {model.Freight}";
                              }
                          }
                          else
                          {
                              logDescription = $"Trip Loss Debited from balance accounts. Deductions exceeded active Freight values. Token#: {model.TokenNo}";
                          }

                          TankerLedger ledgerLog = new TankerLedger()
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
                          };

                          _db.TankerLedgers.Add(ledgerLog);
                      }
                  }

                  // 9. Save Expenses
                  if (uniqueExpenses.Count > 0)
                  {
                      foreach (var item in uniqueExpenses)
                      {
                          item.TripLedgerId = model.Id;
                          item.CreatedAt = pakTime;
                          item.CreatedBy = currentUserId;
                          _db.TripExpenses.Add(item);
                      }
                  }

                  // 10. Cash Ledger Log Entries
                  if (model.AdvanceCash > 0)
                  {
                      currentCashBalance -= model.AdvanceCash;
                      _db.CashLedgers.Add(new CashLedger()
                      {
                          EntryDate = pakTime,
                          Description = $"Trip Ledger Advance Cash Payment | Token#: {model.TokenNo}",
                          Debit = model.AdvanceCash,
                          Credit = 0,
                          Balance = currentCashBalance,
                          CreatedAt = pakTime,
                          CreatedBy = currentUserId
                      });
                  }

                  // Munshiana and Expenses Log
                  if (uniqueExpenses.Count > 0)
                  {
                      foreach (var item in uniqueExpenses)
                      {
                          currentCashBalance -= item.Amount;
                          _db.CashLedgers.Add(new CashLedger()
                          {
                              EntryDate = pakTime,
                              Description = $"Trip Expense Log: {item.ExpenseName} | Token#: {model.TokenNo}",
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
         */
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
        /*  [HttpPost]
          [ValidateAntiForgeryToken]
          public IActionResult EditLedger(TripLedgerVM vm, List<TripExpense> expenses)
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

                  // 2. Fetch Existing Ledger Record (With Expenses)
                  var existingLedger = _db.TripLedgers
                      .Include(x => x.Expenses)
                      .FirstOrDefault(x => x.Id == vm.Id);

                  if (existingLedger == null) return NotFound();

                  // 3. Token Unique Check (Apni current ledger id ko chor kar baki sab par check karein)
                  bool tokenExists = _db.TripLedgers.Any(x => x.TokenNo == vm.TokenNo && x.Id != vm.Id);
                  if (tokenExists)
                  {
                      ModelState.AddModelError("TokenNo", "Token No already exists on another trip.");
                      ViewBag.Products = _db.Products.ToList();
                      ViewBag.Trip = _db.TripEntries.Include(x => x.TankerFk).FirstOrDefault(x => x.Id == vm.TripEntryId);
                      ViewBag.Expenses = _db.TripExpenses.Where(x => x.TripLedgerId == vm.Id).ToList();
                      return View(vm);
                  }

                  // 4. Fetch Trip & Tanker
                  var trip = _db.TripEntries
                      .Include(x => x.TankerFk)
                      .FirstOrDefault(x => x.Id == vm.TripEntryId);

                  if (trip == null) return NotFound();
                  var tanker = trip.TankerFk;

                  // 5. Time Setup
                  var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                  DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                  // =========================================================================
                  // STEP A: REVERSE PURANA EFFECT (Tanker Balance & Cash Ledger)
                  // =========================================================================
                  decimal originalGrandTotal = existingLedger.GrandTotal;

                  // Purana GrandTotal minus kar ke tanker ko wapas pehle wali state par le jao
                  if (tanker != null)
                  {
                      tanker.CurrentBalance -= originalGrandTotal;
                  }

                  // Purana cash required jo is trip ne consume kiya tha, wo wapas add-back karein temporary calculation ke liye
                  var oldExpenses = _db.TripExpenses.Where(x => x.TripLedgerId == existingLedger.Id).ToList();
                  decimal oldTotalExpense = oldExpenses.Sum(x => x.Amount);
                  decimal originalCashRequired = existingLedger.AdvanceCash + existingLedger.Shortage + existingLedger.Munshiana + oldTotalExpense;

                  var lastCashLedger = _db.CashLedgers.OrderByDescending(x => x.Id).FirstOrDefault();
                  decimal currentCashBalance = lastCashLedger?.Balance ?? 0;

                  // Reverse original cash effect mathematically
                  decimal baseCashBeforeThisTrip = currentCashBalance + originalCashRequired;

                  // =========================================================================
                  // STEP B: NAYI CALCULATIONS
                  // =========================================================================
                  var commissionSetup = _db.CommissionSetups.FirstOrDefault(x => x.IsActive);
                  decimal commissionPercent = commissionSetup?.Percentage ?? 0;
                  decimal calculatedCommission = (vm.Freight * commissionPercent) / 100;

                  if (expenses == null) expenses = new List<TripExpense>();
                  var uniqueExpenses = expenses
                      .Where(x => !string.IsNullOrWhiteSpace(x.ExpenseName) && x.Amount > 0)
                      .GroupBy(x => new { ExpenseName = x.ExpenseName.Trim().ToLower(), x.Amount })
                      .Select(g => g.First())
                      .ToList();

                  decimal newTotalExpense = uniqueExpenses.Sum(x => x.Amount);
                  decimal newCashRequired = vm.AdvanceCash + vm.Shortage + vm.Munshiana + newTotalExpense;

                  // Cash Validation Check (Revised Base Cash ke mutabiq check hoga)
                  if (baseCashBeforeThisTrip < newCashRequired)
                  {
                      TempData["Error"] = $"Cash Ledger balance is insufficient for these updates. Available: {baseCashBeforeThisTrip}";
                      return RedirectToAction("EditLedger", new { id = vm.Id });
                  }

                  decimal newTotalDeductions = vm.AdvanceCash + vm.Shortage + calculatedCommission + vm.Munshiana + newTotalExpense;
                  decimal newGrandTotal = vm.Freight - newTotalDeductions;

                  // =========================================================================
                  // STEP C: UPDATE TRIP LEDGER MAIN RECORD
                  // =========================================================================
                  existingLedger.TripDate = vm.TripDate;
                  existingLedger.ProductId = vm.ProductId;
                  existingLedger.Freight = vm.Freight;
                  existingLedger.AdvanceCash = vm.AdvanceCash;
                  existingLedger.Shortage = vm.Shortage;
                  existingLedger.Commission = calculatedCommission;
                  existingLedger.Munshiana = vm.Munshiana;
                  existingLedger.TokenNo = vm.TokenNo;
                  existingLedger.Remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? "N/A" : vm.Remarks;
                  existingLedger.TotalDeductions = newTotalDeductions;
                  existingLedger.GrandTotal = newGrandTotal;
                  existingLedger.AmountPay = newGrandTotal;
                  existingLedger.UpdatedAt = pakTime; // standard tracking field agar entity me ho
                  existingLedger.UpdatedBy = currentUserId;

                  // =========================================================================
                  // STEP D: UPDATE TANKER BALANCE & RE-LOG TANKER LEDGER
                  // =========================================================================
                  if (tanker != null)
                  {
                      // Update Tanker to final new balance
                      tanker.CurrentBalance += newGrandTotal;

                      // Purani TankerLedger log entry ko dhoond kar delete karein ya update karein. 
                      // Standard approach: Purani remove kar ke current running balance ke sath nayi insert karein.
                      var oldTankerLog = _db.TankerLedgers.FirstOrDefault(x => x.ModuleName == "Trip Ledger Add" && x.ReferenceId == existingLedger.Id);
                      if (oldTankerLog != null)
                      {
                          _db.TankerLedgers.Remove(oldTankerLog);
                      }

                      if (newGrandTotal != 0)
                      {
                          decimal creditAmount = newGrandTotal > 0 ? newGrandTotal : 0;
                          decimal debitAmount = newGrandTotal < 0 ? Math.Abs(newGrandTotal) : 0;

                          string logDescription = newGrandTotal > 0
                              ? $"[EDITED] Trip Profit Credited. Token#: {existingLedger.TokenNo}, Freight: {existingLedger.Freight}"
                              : $"[EDITED] Trip Loss Debited. Token#: {existingLedger.TokenNo}, Deductions exceeded Freight.";

                          TankerLedger ledgerLog = new TankerLedger()
                          {
                              TankerId = tanker.Id,
                              TransactionDate = pakTime,
                              ModuleName = "Trip Ledger Add",
                              ReferenceId = existingLedger.Id,
                              Credit = creditAmount,
                              Debit = debitAmount,
                              RunningBalance = tanker.CurrentBalance, // New current balance logged
                              Description = logDescription,
                              CreatedAt = pakTime,
                              CreatedBy = currentUserId
                          };
                          _db.TankerLedgers.Add(ledgerLog);
                      }
                  }

                  // =========================================================================
                  // STEP E: UPDATE EXPENSES
                  // =========================================================================
                  // Purane saare expenses urrayen
                  if (oldExpenses.Any())
                  {
                      _db.TripExpenses.RemoveRange(oldExpenses);
                  }

                  // Naye wale saare fresh insert karein
                  if (uniqueExpenses.Count > 0)
                  {
                      foreach (var item in uniqueExpenses)
                      {
                          item.TripLedgerId = existingLedger.Id;
                          item.CreatedAt = pakTime;
                          item.CreatedBy = currentUserId;
                          _db.TripExpenses.Add(item);
                      }
                  }

                  // =========================================================================
                  // STEP F: CASH LEDGER FRESH LOG ENTRIES (Reverse + New Log Strategy)
                  // =========================================================================
                  // Tip: Cash ledger me direct updates purani logs par karne se running balances out ho jati hain.
                  // Is liye hum purane entries ke counter-effect dalne ki bajaye unko base par set kar chuke hain.
                  // Ab hum new records insert karenge with updated final cash balance tracker.

                  decimal runningCashTracker = baseCashBeforeThisTrip;

                  if (existingLedger.AdvanceCash > 0)
                  {
                      runningCashTracker -= existingLedger.AdvanceCash;
                      _db.CashLedgers.Add(new CashLedger()
                      {
                          EntryDate = pakTime,
                          Description = $"[EDITED] Trip Ledger Advance Cash | Token#: {existingLedger.TokenNo}",
                          Debit = existingLedger.AdvanceCash,
                          Credit = 0,
                          Balance = runningCashTracker,
                          CreatedAt = pakTime,
                          CreatedBy = currentUserId
                      });
                  }

                  if (existingLedger.Shortage > 0)
                  {
                      runningCashTracker -= existingLedger.Shortage;
                      _db.CashLedgers.Add(new CashLedger()
                      {
                          EntryDate = pakTime,
                          Description = $"[EDITED] Trip Ledger Shortage | Token#: {existingLedger.TokenNo}",
                          Debit = existingLedger.Shortage,
                          Credit = 0,
                          Balance = runningCashTracker,
                          CreatedAt = pakTime,
                          CreatedBy = currentUserId
                      });
                  }

                  if (existingLedger.Munshiana > 0)
                  {
                      runningCashTracker -= existingLedger.Munshiana;
                      _db.CashLedgers.Add(new CashLedger()
                      {
                          EntryDate = pakTime,
                          Description = $"[EDITED] Trip Ledger Munshiana | Token#: {existingLedger.TokenNo}",
                          Debit = existingLedger.Munshiana,
                          Credit = 0,
                          Balance = runningCashTracker,
                          CreatedAt = pakTime,
                          CreatedBy = currentUserId
                      });
                  }

                  if (uniqueExpenses.Count > 0)
                  {
                      foreach (var item in uniqueExpenses)
                      {
                          runningCashTracker -= item.Amount;
                          _db.CashLedgers.Add(new CashLedger()
                          {
                              EntryDate = pakTime,
                              Description = $"[EDITED] Trip Expense: {item.ExpenseName} | Token#: {existingLedger.TokenNo}",
                              Debit = item.Amount,
                              Credit = 0,
                              Balance = runningCashTracker,
                              CreatedAt = pakTime,
                              CreatedBy = currentUserId
                          });
                      }
                  }

                  // Final Database Save Changes & Transaction Commit
                  _db.SaveChanges();
                  transaction.Commit();

                  TempData["edit_ledger"] = "Ledger Updated Successfully With Double Entry Logs";
                  return RedirectToAction("Index");
              }
              catch (Exception ex)
              {
                  transaction.Rollback();
                  TempData["Error"] = ex.Message;
                  return RedirectToAction("EditLedger", new { id = vm.Id });
              }
          }
        */

        /* [HttpPost]
         [ValidateAntiForgeryToken]
         public IActionResult EditLedger(TripLedgerVM vm, List<TripExpense> expenses)
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

                 // 2. Fetch Existing Ledger Record (With Expenses)
                 var existingLedger = _db.TripLedgers
                     .Include(x => x.Expenses)
                     .FirstOrDefault(x => x.Id == vm.Id);

                 if (existingLedger == null) return NotFound();

                 // 3. Token Unique Check
                 bool tokenExists = _db.TripLedgers.Any(x => x.TokenNo == vm.TokenNo && x.Id != vm.Id);
                 if (tokenExists)
                 {
                     ModelState.AddModelError("TokenNo", "Token No already exists on another trip.");
                     ViewBag.Products = _db.Products.ToList();
                     ViewBag.Trip = _db.TripEntries.Include(x => x.TankerFk).FirstOrDefault(x => x.Id == vm.TripEntryId);
                     ViewBag.Expenses = _db.TripExpenses.Where(x => x.TripLedgerId == vm.Id).ToList();
                     return View(vm);
                 }

                 // 4. Fetch Trip & Tanker
                 var trip = _db.TripEntries
                     .Include(x => x.TankerFk)
                     .FirstOrDefault(x => x.Id == vm.TripEntryId);

                 if (trip == null) return NotFound();
                 var tanker = trip.TankerFk;

                 // 5. Time Setup
                 var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                 DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                 // =========================================================================
                 // STEP A: REVERSE PURANA EFFECT (Tanker Balance & Calculation Setup)
                 // =========================================================================
                 decimal originalGrandTotal = existingLedger.GrandTotal;
                 decimal oldBalanceBeforeReverse = tanker?.CurrentBalance ?? 0;

                 // Purana GrandTotal minus kar ke tanker ko wapas pehle wali state par le jao
                 if (tanker != null)
                 {
                     tanker.CurrentBalance -= originalGrandTotal;
                 }

                 // 🔥 REQUIREMENT REVISED: Shortage aur Commission cash ledger me nahi jate.
                 // Isliye purane cash required me sirf Advance, Munshiana, aur old expenses sum honge.
                 var oldExpenses = _db.TripExpenses.Where(x => x.TripLedgerId == existingLedger.Id).ToList();
                 decimal oldTotalExpense = oldExpenses.Sum(x => x.Amount);
                 decimal originalCashRequired = existingLedger.AdvanceCash + existingLedger.Munshiana + oldTotalExpense;

                 var lastCashLedger = _db.CashLedgers.OrderByDescending(x => x.Id).FirstOrDefault();
                 decimal currentCashBalance = lastCashLedger?.Balance ?? 0;

                 // Reverse original actual cash effect mathematically
                 decimal baseCashBeforeThisTrip = currentCashBalance + originalCashRequired;

                 // =========================================================================
                 // STEP B: NAYI CALCULATIONS (Auto Commission Fetching)
                 // =========================================================================
                 var commissionSetup = _db.CommissionSetups.FirstOrDefault(x => x.IsActive);
                 decimal commissionPercent = commissionSetup?.Percentage ?? 0;
                 decimal calculatedCommission = (vm.Freight * commissionPercent) / 100;

                 if (expenses == null) expenses = new List<TripExpense>();
                 var uniqueExpenses = expenses
                     .Where(x => !string.IsNullOrWhiteSpace(x.ExpenseName) && x.Amount > 0)
                     .GroupBy(x => new { ExpenseName = x.ExpenseName.Trim().ToLower(), x.Amount })
                     .Select(g => g.First())
                     .ToList();

                 decimal newTotalExpense = uniqueExpenses.Sum(x => x.Amount);

                 // 🔥 Naya Cash Required validation checklist (Excluding Shortage & Commission)
                 decimal newCashRequired = vm.AdvanceCash + vm.Munshiana + newTotalExpense;

                 // Cash Validation Check (Revised Base Cash ke mutabiq check hoga)
                 if (baseCashBeforeThisTrip < newCashRequired)
                 {
                     TempData["Error"] = $"Cash Ledger balance is insufficient for these updates. Available: {baseCashBeforeThisTrip}";
                     return RedirectToAction("EditLedger", new { id = vm.Id });
                 }

                 decimal newTotalDeductions = vm.AdvanceCash + vm.Shortage + calculatedCommission + vm.Munshiana + newTotalExpense;
                 decimal newGrandTotal = vm.Freight - newTotalDeductions;

                 // =========================================================================
                 // STEP C: UPDATE TRIP LEDGER MAIN RECORD (No duplication, updates same row)
                 // =========================================================================
                 existingLedger.TripDate = vm.TripDate;
                 existingLedger.ProductId = vm.ProductId;
                 existingLedger.Freight = vm.Freight;
                 existingLedger.AdvanceCash = vm.AdvanceCash;
                 existingLedger.Shortage = vm.Shortage;
                 existingLedger.Commission = calculatedCommission; // Auto assigned from db configuration
                 existingLedger.Munshiana = vm.Munshiana;
                 existingLedger.TokenNo = vm.TokenNo;
                 existingLedger.Remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? "N/A" : vm.Remarks;
                 existingLedger.TotalDeductions = newTotalDeductions;
                 existingLedger.GrandTotal = newGrandTotal;
                 existingLedger.AmountPay = newGrandTotal;
                 existingLedger.UpdatedAt = pakTime;
                 existingLedger.UpdatedBy = currentUserId;

                 // =========================================================================
                 // STEP D: UPDATE TANKER BALANCE & RE-LOG TANKER LEDGER WITH DUES TRACKING
                 // =========================================================================
                 if (tanker != null)
                 {
                     // Tanker balance context before final assignment
                     decimal baseTankerBalanceWithoutThisTrip = tanker.CurrentBalance;

                     // Apply new Grand Total
                     tanker.CurrentBalance += newGrandTotal;

                     // Purani TankerLedger log entry ko dhoond kar delete karein
                     var oldTankerLog = _db.TankerLedgers.FirstOrDefault(x => x.ModuleName == "Trip Ledger Add" && x.ReferenceId == existingLedger.Id);
                     if (oldTankerLog != null)
                     {
                         _db.TankerLedgers.Remove(oldTankerLog);
                     }

                     if (newGrandTotal != 0)
                     {
                         decimal creditAmount = newGrandTotal > 0 ? newGrandTotal : 0;
                         decimal debitAmount = newGrandTotal < 0 ? Math.Abs(newGrandTotal) : 0;

                         // 🔥 REQUIREMENT: Dues Checking Logic inside Edit Module
                         string logDescription = "";
                         if (newGrandTotal > 0)
                         {
                             if (baseTankerBalanceWithoutThisTrip < 0) // Tanker was in dues liability state
                             {
                                 if (tanker.CurrentBalance >= 0)
                                 {
                                     logDescription = $"[EDITED] Trip Profit applied: Outstanding dues of {Math.Abs(baseTankerBalanceWithoutThisTrip)} completely cleared. Excess moved to profile profit. Token#: {existingLedger.TokenNo}";
                                 }
                                 else
                                 {
                                     logDescription = $"[EDITED] Trip Profit applied: Outstanding dues reduced from {Math.Abs(baseTankerBalanceWithoutThisTrip)} to {Math.Abs(tanker.CurrentBalance)}. Token#: {existingLedger.TokenNo}";
                                 }
                             }
                             else // Already in safe profit zone
                             {
                                 logDescription = $"[EDITED] Trip Profit Credited. Token#: {existingLedger.TokenNo}, Freight: {existingLedger.Freight}";
                             }
                         }
                         else
                         {
                             logDescription = $"[EDITED] Trip Loss Debited. Deductions exceeded available Freight variables. Token#: {existingLedger.TokenNo}";
                         }

                         TankerLedger ledgerLog = new TankerLedger()
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
                         };
                         _db.TankerLedgers.Add(ledgerLog);
                     }
                 }

                 // =========================================================================
                 // STEP E: UPDATE EXPENSES (Removes old array dependencies to prevent duplication)
                 // =========================================================================
                 if (oldExpenses.Any())
                 {
                     _db.TripExpenses.RemoveRange(oldExpenses);
                 }

                 if (uniqueExpenses.Count > 0)
                 {
                     foreach (var item in uniqueExpenses)
                     {
                         item.TripLedgerId = existingLedger.Id;
                         item.CreatedAt = pakTime;
                         item.CreatedBy = currentUserId;
                         _db.TripExpenses.Add(item);
                     }
                 }

                 // =========================================================================
                 // STEP F: CASH LEDGER FRESH LOG ENTRIES (No duplicate trace, clean running state)
                 // =========================================================================
                 decimal runningCashTracker = baseCashBeforeThisTrip;

                 if (existingLedger.AdvanceCash > 0)
                 {
                     runningCashTracker -= existingLedger.AdvanceCash;
                     _db.CashLedgers.Add(new CashLedger()
                     {
                         EntryDate = pakTime,
                         Description = $"[EDITED] Trip Ledger Advance Cash | Token#: {existingLedger.TokenNo}",
                         Debit = existingLedger.AdvanceCash,
                         Credit = 0,
                         Balance = runningCashTracker,
                         CreatedAt = pakTime,
                         CreatedBy = currentUserId
                     });
                 }

                 // 🔥 OMITTED ACCORDING TO REQUIREMENT: Shortage aur Commission Cash Ledger se completely block hain.

                 if (existingLedger.Munshiana > 0)
                 {
                     runningCashTracker -= existingLedger.Munshiana;
                     _db.CashLedgers.Add(new CashLedger()
                     {
                         EntryDate = pakTime,
                         Description = $"[EDITED] Trip Ledger Munshiana | Token#: {existingLedger.TokenNo}",
                         Debit = existingLedger.Munshiana,
                         Credit = 0,
                         Balance = runningCashTracker,
                         CreatedAt = pakTime,
                         CreatedBy = currentUserId
                     });
                 }

                 if (uniqueExpenses.Count > 0)
                 {
                     foreach (var item in uniqueExpenses)
                     {
                         runningCashTracker -= item.Amount;
                         _db.CashLedgers.Add(new CashLedger()
                         {
                             EntryDate = pakTime,
                             Description = $"[EDITED] Trip Expense: {item.ExpenseName} | Token#: {existingLedger.TokenNo}",
                             Debit = item.Amount,
                             Credit = 0,
                             Balance = runningCashTracker,
                             CreatedAt = pakTime,
                             CreatedBy = currentUserId
                         });
                     }
                 }

                 // Final Database Save Changes & Transaction Commit
                 _db.SaveChanges();
                 transaction.Commit();

                 TempData["edit_ledger"] = "Ledger Updated Successfully With Clean Audit Logs";
                 return RedirectToAction("Index");
             }
             catch (Exception ex)
             {
                 transaction.Rollback();
                 TempData["Error"] = ex.Message;
                 return RedirectToAction("EditLedger", new { id = vm.Id });
             }
         }
 */

        /* [HttpPost]
         [ValidateAntiForgeryToken]
         public IActionResult EditLedger(TripLedgerVM vm, List<TripExpense> expenses)
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

                 // 2. Fetch Existing Ledger Record (With Expenses)
                 var existingLedger = _db.TripLedgers
                     .Include(x => x.Expenses)
                     .FirstOrDefault(x => x.Id == vm.Id);

                 if (existingLedger == null) return NotFound();

                 // Pehle se save token ko check karne ke liye backup rakhein
                 string oldTokenNo = existingLedger.TokenNo;

                 // 3. Token Unique Check
                 bool tokenExists = _db.TripLedgers.Any(x => x.TokenNo == vm.TokenNo && x.Id != vm.Id);
                 if (tokenExists)
                 {
                     ModelState.AddModelError("TokenNo", "Token No already exists on another trip.");
                     ViewBag.Products = _db.Products.ToList();
                     ViewBag.Trip = _db.TripEntries.Include(x => x.TankerFk).FirstOrDefault(x => x.Id == vm.TripEntryId);
                     ViewBag.Expenses = _db.TripExpenses.Where(x => x.TripLedgerId == vm.Id).ToList();
                     return View(vm);
                 }

                 // 4. Fetch Trip & Tanker
                 var trip = _db.TripEntries
                     .Include(x => x.TankerFk)
                     .FirstOrDefault(x => x.Id == vm.TripEntryId);

                 if (trip == null) return NotFound();
                 var tanker = trip.TankerFk;

                 // 5. Time Setup
                 var tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
                 DateTime pakTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                 // =========================================================================
                 // STEP A: REVERSE PURANA EFFECT & FETCH OLD CASH LOGS
                 // =========================================================================
                 decimal originalGrandTotal = existingLedger.GrandTotal;

                 if (tanker != null)
                 {
                     tanker.CurrentBalance -= originalGrandTotal;
                 }

                 var oldExpenses = _db.TripExpenses.Where(x => x.TripLedgerId == existingLedger.Id).ToList();
                 decimal oldTotalExpense = oldExpenses.Sum(x => x.Amount);
                 decimal originalCashRequired = existingLedger.AdvanceCash + existingLedger.Munshiana + oldTotalExpense;

                 var lastCashLedger = _db.CashLedgers.OrderByDescending(x => x.Id).FirstOrDefault();
                 decimal currentCashBalance = lastCashLedger?.Balance ?? 0;

                 // Mathematically baseline cash trace balance
                 decimal baseCashBeforeThisTrip = currentCashBalance + originalCashRequired;

                 // 🔥 FIX: Is Token se related jitne bhi CashLedger ke purane records hain, unko fetch karein (Nayi ID banana se rokne ke liye)
                 // Hum purane token aur naye token dono ka search check lagate hain safety ke liye
                 var oldCashEntries = _db.CashLedgers
                     .Where(x => x.Description.Contains($"Token#: {oldTokenNo}") || x.Description.Contains($"Token#: {vm.TokenNo}"))
                     .OrderBy(x => x.Id)
                     .ToList();

                 // =========================================================================
                 // STEP B: NAYI CALCULATIONS
                 // =========================================================================
                 var commissionSetup = _db.CommissionSetups.FirstOrDefault(x => x.IsActive);
                 decimal commissionPercent = commissionSetup?.Percentage ?? 0;
                 decimal calculatedCommission = (vm.Freight * commissionPercent) / 100;

                 if (expenses == null) expenses = new List<TripExpense>();
                 var uniqueExpenses = expenses
                     .Where(x => !string.IsNullOrWhiteSpace(x.ExpenseName) && x.Amount > 0)
                     .GroupBy(x => new { ExpenseName = x.ExpenseName.Trim().ToLower(), x.Amount })
                     .Select(g => g.First())
                     .ToList();

                 decimal newTotalExpense = uniqueExpenses.Sum(x => x.Amount);
                 decimal newCashRequired = vm.AdvanceCash + vm.Munshiana + newTotalExpense;

                 if (baseCashBeforeThisTrip < newCashRequired)
                 {
                     TempData["Error"] = $"Cash Ledger balance is insufficient for these updates. Available: {baseCashBeforeThisTrip}";
                     return RedirectToAction("EditLedger", new { id = vm.Id });
                 }

                 decimal newTotalDeductions = vm.AdvanceCash + vm.Shortage + calculatedCommission + vm.Munshiana + newTotalExpense;
                 decimal newGrandTotal = vm.Freight - newTotalDeductions;

                 // =========================================================================
                 // STEP C: UPDATE TRIP LEDGER MAIN RECORD (SAME ROW UPDATED)
                 // =========================================================================
                 existingLedger.TripDate = vm.TripDate;
                 existingLedger.ProductId = vm.ProductId;
                 existingLedger.Freight = vm.Freight;
                 existingLedger.AdvanceCash = vm.AdvanceCash;
                 existingLedger.Shortage = vm.Shortage;
                 existingLedger.Commission = calculatedCommission;
                 existingLedger.Munshiana = vm.Munshiana;
                 existingLedger.TokenNo = vm.TokenNo;
                 existingLedger.Remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? "N/A" : vm.Remarks;
                 existingLedger.TotalDeductions = newTotalDeductions;
                 existingLedger.GrandTotal = newGrandTotal;
                 existingLedger.AmountPay = newGrandTotal;
                 existingLedger.UpdatedAt = pakTime;
                 existingLedger.UpdatedBy = currentUserId;

                 // =========================================================================
                 // STEP D: UPDATE TANKER BALANCE & RE-LOG TANKER LEDGER
                 // =========================================================================
                 if (tanker != null)
                 {
                     decimal baseTankerBalanceWithoutThisTrip = tanker.CurrentBalance;
                     tanker.CurrentBalance += newGrandTotal;

                     var oldTankerLog = _db.TankerLedgers.FirstOrDefault(x => x.ModuleName == "Trip Ledger Add" && x.ReferenceId == existingLedger.Id);
                     if (oldTankerLog != null)
                     {
                         _db.TankerLedgers.Remove(oldTankerLog);
                     }

                     if (newGrandTotal != 0)
                     {
                         decimal creditAmount = newGrandTotal > 0 ? newGrandTotal : 0;
                         decimal debitAmount = newGrandTotal < 0 ? Math.Abs(newGrandTotal) : 0;

                         string logDescription = "";
                         if (newGrandTotal > 0)
                         {
                             if (baseTankerBalanceWithoutThisTrip < 0)
                             {
                                 if (tanker.CurrentBalance >= 0)
                                     logDescription = $"[EDITED] Trip Profit applied: Outstanding dues of {Math.Abs(baseTankerBalanceWithoutThisTrip)} completely cleared. Token#: {existingLedger.TokenNo}";
                                 else
                                     logDescription = $"[EDITED] Trip Profit applied: Outstanding dues reduced to {Math.Abs(tanker.CurrentBalance)}. Token#: {existingLedger.TokenNo}";
                             }
                             else
                             {
                                 logDescription = $"[EDITED] Trip Profit Credited. Token#: {existingLedger.TokenNo}, Freight: {existingLedger.Freight}";
                             }
                         }
                         else
                         {
                             logDescription = $"[EDITED] Trip Loss Debited. Token#: {existingLedger.TokenNo}";
                         }

                         TankerLedger ledgerLog = new TankerLedger()
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
                         };
                         _db.TankerLedgers.Add(ledgerLog);
                     }
                 }

                 // =========================================================================
                 // STEP E: UPDATE EXPENSES
                 // =========================================================================
                 if (oldExpenses.Any())
                 {
                     _db.TripExpenses.RemoveRange(oldExpenses);
                 }

                 if (uniqueExpenses.Count > 0)
                 {
                     foreach (var item in uniqueExpenses)
                     {
                         item.TripLedgerId = existingLedger.Id;
                         item.CreatedAt = pakTime;
                         item.CreatedBy = currentUserId;
                         _db.TripExpenses.Add(item);
                     }
                 }

                 // =========================================================================
                 // STEP F: CASH LEDGER IN-PLACE UPDATE (NO NEW ROWS LOGIC)
                 // =========================================================================
                 decimal runningCashTracker = baseCashBeforeThisTrip;

                 // Ek data matrix banayen jo naye cash transitions ka sequence hold karega
                 var targetCashLogs = new List<(string Description, decimal Amount)>();

                 if (existingLedger.AdvanceCash > 0)
                 {
                     targetCashLogs.Add(($"[EDITED] Trip Ledger Advance Cash | Token#: {existingLedger.TokenNo}", existingLedger.AdvanceCash));
                 }

                 if (existingLedger.Munshiana > 0)
                 {
                     targetCashLogs.Add(($"[EDITED] Trip Ledger Munshiana | Token#: {existingLedger.TokenNo}", existingLedger.Munshiana));
                 }

                 if (uniqueExpenses.Count > 0)
                 {
                     foreach (var item in uniqueExpenses)
                     {
                         targetCashLogs.Add(($"[EDITED] Trip Expense: {item.ExpenseName} | Token#: {existingLedger.TokenNo}", item.Amount));
                     }
                 }

                 // 🔥 STRATEGY: Pehle se majood rows ko map karenge taake primary key IDs change na hon!
                 for (int i = 0; i < targetCashLogs.Count; i++)
                 {
                     runningCashTracker -= targetCashLogs[i].Amount;

                     if (i < oldCashEntries.Count)
                     {
                         // 1. Agar purani row exist karti hai, toh uski details overwrite karo (No new ID created)
                         var existingCashRow = oldCashEntries[i];
                         existingCashRow.EntryDate = pakTime;
                         existingCashRow.Description = targetCashLogs[i].Description;
                         existingCashRow.Debit = targetCashLogs[i].Amount;
                         existingCashRow.Credit = 0;
                         existingCashRow.Balance = runningCashTracker;
                         existingCashRow.CreatedAt = pakTime; // tracking updated context
                         existingCashRow.CreatedBy = currentUserId;

                         _db.CashLedgers.Update(existingCashRow);
                     }
                     else
                     {
                         // 2. Agar user ne edit karte waqt naye expenses barha diye hain aur purani rows kam par gayi hain, tabhi sirf naya log generate hoga
                         _db.CashLedgers.Add(new CashLedger()
                         {
                             EntryDate = pakTime,
                             Description = targetCashLogs[i].Description,
                             Debit = targetCashLogs[i].Amount,
                             Credit = 0,
                             Balance = runningCashTracker,
                             CreatedAt = pakTime,
                             CreatedBy = currentUserId
                         });
                     }
                 }

                 // 3. Agar user ne edit karte waqt items kam kar diye hain, toh bache hue purane un-wanted rows ko remove kar dein
                 if (oldCashEntries.Count > targetCashLogs.Count)
                 {
                     for (int i = targetCashLogs.Count; i < oldCashEntries.Count; i++)
                     {
                         _db.CashLedgers.Remove(oldCashEntries[i]);
                     }
                 }

                 // Database Save Changes & Transaction Commit
                 _db.SaveChanges();
                 transaction.Commit();

                 TempData["edit_ledger"] = "Ledger Updated Successfully Without Duplication";
                 return RedirectToAction("Index");
             }
             catch (Exception ex)
             {
                 transaction.Rollback();
                 TempData["Error"] = ex.Message;
                 return RedirectToAction("EditLedger", new { id = vm.Id });
             }
         }
 */

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