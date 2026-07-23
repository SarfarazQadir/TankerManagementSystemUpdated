using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.ViewModels;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AuthorizeModule]
    public class MonthlyBillController : Controller
    {
        private readonly ApplicationDbContext _db;

        public MonthlyBillController(ApplicationDbContext db)
        {
            _db = db;
        }

        // ============================
        // INDEX PAGE
        // ============================
        public IActionResult Index()
        {
            ViewBag.Tankers = _db.Tankers
                .Include(x => x.Owner)
                .ToList();

            return View();
        }

        // ============================
        // GENERATE MONTHLY BILL
        // ============================               
        public IActionResult Generate(int tankerId, int month, int year)
        {
            var tanker = _db.Tankers
                .Include(x => x.Owner)
                .FirstOrDefault(x => x.Id == tankerId);

            if (tanker == null)
                return NotFound();

            DateTime targetMonthStartDate = new DateTime(year, month, 1);

            var tripEntries = _db.TripEntries
                .Where(x => x.TankerId == tankerId && x.LoadDate.Month == month && x.LoadDate.Year == year)
                .OrderBy(x => x.LoadDate)
                .ToList();

            var entryIds = tripEntries.Select(e => e.Id).ToList();

            var ledgers = _db.TripLedgers
                .Include(x => x.Product)
                .Where(x => x.TripEntryId != null && entryIds.Contains(x.TripEntryId))
                .ToList();

            var cashEntries = _db.CashLedgers
                .Where(x => x.TankerId == tankerId && x.EntryDate.Month == month && x.EntryDate.Year == year)
                .OrderBy(x => x.EntryDate)
                .ToList();

            var atsEntries = _db.AtsPsoEntries
                .Where(x => x.TankerId == tankerId && x.EntryDate.Month == month && x.EntryDate.Year == year)
                .OrderBy(x => x.EntryDate)
                .ToList();

            decimal totalFreight = 0;
            decimal totalAdvance = 0;
            decimal totalShortage = 0;
            decimal totalShortageLitre = 0;
            decimal totalCommission = 0;
            decimal totalAts = atsEntries.Sum(x => x.Amount);
            decimal totalCashCredit = cashEntries.Sum(x => x.Credit);
            decimal totalCashDebit = cashEntries.Sum(x => x.Debit);

            var rows = new List<StatementRowVM>();

            foreach (var entry in tripEntries)
            {
                var ledger = ledgers.FirstOrDefault(l => l.TripEntryId == entry.Id);

                decimal freight = ledger?.Freight ?? 0;
                decimal shortage = ledger?.Shortage ?? 0;
                decimal shortageLitre = ledger?.ShortageLiters ?? 0;

                decimal munshiana = ledger?.Munshiana ?? 0;
                decimal advance = entry.AdvanceCash + munshiana;
                decimal commission = ledger?.Commission ?? 0;

                totalFreight += freight;
                totalAdvance += advance;
                totalShortage += shortage;
                totalShortageLitre += shortageLitre;
                totalCommission += commission;

                decimal totalDeduction = advance + shortage + commission;

                rows.Add(new StatementRowVM
                {
                    Date = entry.LoadDate,
                    RowType = "TRIP",
                    Details = $"{entry.FromLocation} to {entry.ToLocation} {(ledger?.Product != null ? $"({ledger.Product.ProductName})" : "")}",
                    Freight = freight,
                    Deduction = totalDeduction,
                    Net = freight - totalDeduction,
                    TripRef = ledger != null ? ledger : new TripLedger { Shortage = shortage, AdvanceCash = entry.AdvanceCash, ShortageLiters = shortageLitre }
                });
            }

            foreach (var cash in cashEntries)
            {
                rows.Add(new StatementRowVM
                {
                    Date = cash.EntryDate,
                    RowType = "CASH",
                    Details = cash.Description,
                    Freight = cash.Credit,
                    Deduction = cash.Debit,
                    Net = cash.Credit - cash.Debit,
                    TripRef = null
                });
            }

            foreach (var ats in atsEntries)
            {
                rows.Add(new StatementRowVM
                {
                    Date = ats.EntryDate,
                    RowType = "ATS",
                    Details = ats.Description,
                    Freight = 0,
                    Deduction = ats.Amount,
                    Net = 0 - ats.Amount,
                    TripRef = null
                });
            }

            var orderedRows = rows.OrderBy(x => x.Date).ToList();
            decimal previousBalance = 0;

            var lastLedgerBeforeMonth = _db.TankerLedgers
                .Where(x => x.TankerId == tankerId && x.TransactionDate < targetMonthStartDate)
                .OrderByDescending(x => x.TransactionDate)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();

            if (lastLedgerBeforeMonth != null)
            {
                previousBalance = lastLedgerBeforeMonth.RunningBalance;
            }
            // FIX Issue 06: If no ledger entry exists before the billing month - Done by AntiGravity on 2026-07-18 08:15 PST
            // previousBalance stays 0. The old fallback to the first-ever entry
            // caused double-counting when that entry fell within the billing month.

            ViewBag.OpeningBalance = previousBalance;
            ViewBag.TotalFreight = totalFreight;
            ViewBag.TotalAdvance = totalAdvance;
            ViewBag.TotalShortage = totalShortage;
            ViewBag.TotalShortageLitre = totalShortageLitre;
            ViewBag.TotalCommission = totalCommission;
            ViewBag.TotalAts = totalAts;
            ViewBag.TotalCashCredit = totalCashCredit;
            ViewBag.TotalCashDebit = totalCashDebit;

            ViewBag.Tanker = tanker;
            ViewBag.Month = month;
            ViewBag.Year = year;

            // Modified by AI
            // Date: 2026-07-21
            // Reason: C-04 — Added ClosingBalance calculation so the monthly bill view
            // can display a reconcilable closing balance.
            // Formula: ClosingBalance = previousBalance + sum of all row Net values
            // This works because each StatementRowVM.Net stores exactly what was posted to
            // TankerLedger for that entry (Trip: GrandTotal, ATS: -Amount, Cash: Credit-Debit).
            // The ClosingBalance can be cross-checked against the actual TankerLedger.RunningBalance
            // of the last entry in the billing month to verify financial consistency.
            decimal netMovement = orderedRows.Sum(r => r.Net);
            decimal closingBalance = previousBalance + netMovement;

            ViewBag.NetMovement = netMovement;
            ViewBag.ClosingBalance = closingBalance;

            return View(orderedRows);
        }
    }
}