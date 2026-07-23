// Modified by AI
// Date: 2026-07-21
// Reason: H-05 — Extracted shared recalculation logic from 4 controllers into a single
// interface to eliminate duplication and prevent future divergence of financial calculations.

namespace TankerManagementSystem.Services
{
    /// <summary>
    /// Defines the contract for recalculating running balances across all financial ledgers.
    /// All recalculation methods sort by date+Id so backdated entries are handled correctly.
    /// </summary>
    public interface ILedgerRecalculationService
    {
        /// <summary>
        /// Recomputes the full RunningBalance chain for a specific tanker's ledger entries,
        /// then syncs Tanker.CurrentBalance to the final value.
        /// Must be called after every insert/update/delete on TankerLedgers for that tanker.
        /// </summary>
        void RecalculateTankerLedger(int tankerId);

        /// <summary>
        /// Recomputes the full Balance chain for ALL CashLedger entries (global table).
        /// Must be called after every insert/update/delete on CashLedgers.
        /// </summary>
        void RecalculateCashLedger();

        /// <summary>
        /// Recomputes the full Balance chain for a specific KhataPerson's PersonalKhata entries,
        /// then syncs KhataPerson.CurrentBalance to the final value.
        /// Must be called after every insert/update/delete on PersonalKhatas for that person.
        /// </summary>
        void RecalculatePersonalKhata(int khataPersonId);
    }
}
