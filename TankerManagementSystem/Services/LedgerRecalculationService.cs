// Modified by AI
// Date: 2026-07-21
// Reason: H-05 — Single authoritative implementation of all ledger recalculation logic.
// Previously this same code was duplicated as private methods inside CashLedgerController,
// TripLedgerController, AtsPsoController, and TankerController.
// C-01/C-02 fix: RecalculateCashLedger() is now accessible to TripEntryController,
// which previously had no way to call it.

using TankerManagementSystem.Models;

namespace TankerManagementSystem.Services
{
    /// <summary>
    /// Provides recalculation of running balances for TankerLedger, CashLedger,
    /// and PersonalKhata tables. Registered as Scoped in DI (one instance per request).
    /// All methods operate on the same DbContext instance as the calling controller.
    /// </summary>
    public class LedgerRecalculationService : ILedgerRecalculationService
    {
        private readonly ApplicationDbContext _db;

        public LedgerRecalculationService(ApplicationDbContext db)
        {
            _db = db;
        }

        // ==========================================
        // TANKER LEDGER RECALCULATION
        // Orders all entries for the given tanker by TransactionDate then Id,
        // recomputes the running balance chain (Credit - Debit), and syncs
        // Tanker.CurrentBalance to the final (latest) running balance.
        // This correctly handles backdated entries without corrupting the chain.
        // ==========================================
        public void RecalculateTankerLedger(int tankerId)
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

        // ==========================================
        // CASH LEDGER RECALCULATION
        // Orders ALL CashLedger entries globally by EntryDate then Id,
        // recomputes the running Balance chain. No per-record scope —
        // this is a single global cash account.
        // ==========================================
        public void RecalculateCashLedger()
        {
            var rows = _db.CashLedgers
                .OrderBy(x => x.EntryDate)
                .ThenBy(x => x.Id)
                .ToList();

            decimal running = 0;
            foreach (var row in rows)
            {
                running += (row.Credit - row.Debit);
                row.Balance = running;
            }

            _db.SaveChanges();
        }

        // ==========================================
        // PERSONAL KHATA RECALCULATION
        // Orders all PersonalKhata entries for the given person by EntryDate then Id,
        // recomputes the running Balance chain (AddAmount - MinusAmount), and syncs
        // KhataPerson.CurrentBalance to the final running balance.
        // ==========================================
        public void RecalculatePersonalKhata(int khataPersonId)
        {
            var rows = _db.PersonalKhatas
                .Where(x => x.KhataPersonId == khataPersonId)
                .OrderBy(x => x.EntryDate)
                .ThenBy(x => x.Id)
                .ToList();

            decimal running = 0;
            foreach (var row in rows)
            {
                running += (row.AddAmount - row.MinusAmount);
                row.Balance = running;
            }

            var person = _db.KhataPersons.FirstOrDefault(p => p.Id == khataPersonId);
            if (person != null)
            {
                person.CurrentBalance = running;
            }

            _db.SaveChanges();
        }
    }
}
