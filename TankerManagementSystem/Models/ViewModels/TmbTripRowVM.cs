using System;
using System.Collections.Generic;
using System.Linq;

namespace TankerManagementSystem.Models.ViewModels
{
    public class TmbTripRowVM
    {
        public DateTime LoadingDate { get; set; }
        public string Destination { get; set; }
        public decimal ShortageLiter { get; set; }
        public decimal ShortageAmount { get; set; }
        public decimal Advance { get; set; }
        public decimal Freight { get; set; }
    }

    public class TmbLedgerRowVM
    {
        public DateTime Date { get; set; }
        public string Detail { get; set; }
        public decimal DR { get; set; }
        public decimal CR { get; set; }
        public decimal Balance { get; set; }
    }

    public class TmbOtherAdvanceVM
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }
    }

    public class TmbBillVM
    {
        // Header
        public string TankerNo { get; set; }          // e.g. "TMB-574"
        public string TankerCapacity { get; set; }     // e.g. "40-KL"
        public string MonthYearLabel { get; set; }      // e.g. "Jun-25"
        public string BillTitle { get; set; } = "TLS Monthly Bill PSO";
        public string CompanyName { get; set; } = "MALIK BROTHERS";
        public string LogoPath { get; set; } = "~/img/flyeasy.png";

        // Trips
        public List<TmbTripRowVM> Trips { get; set; } = new();
        public decimal TotalShortageLiter => Trips.Sum(x => x.ShortageLiter);
        public decimal TotalShortageAmount => Trips.Sum(x => x.ShortageAmount);
        public decimal TotalAdvance => Trips.Sum(x => x.Advance);
        public decimal TotalFreight => Trips.Sum(x => x.Freight);

        // Cash Ledger detail
        public List<TmbLedgerRowVM> LedgerEntries { get; set; } = new();
        public decimal TotalDR => LedgerEntries.Sum(x => x.DR);
        public decimal TotalCR => LedgerEntries.Sum(x => x.CR);
        public decimal LedgerNetBalance => TotalCR - TotalDR;

        // Summary calculation block
        public decimal CommissionPercent { get; set; }
        public decimal Commission { get; set; }
        public decimal BalanceAfterCommission => TotalFreight - Commission;
        public decimal BalanceAfterShortageAdvance =>
            BalanceAfterCommission - TotalShortageAmount - TotalAdvance;
        public decimal Anbam => BalanceAfterShortageAdvance - TotalDR;
        public decimal PreviousBalance { get; set; }
        public decimal CurrentBalance => PreviousBalance + Anbam;

        // Other advances breakdown (Tax, Tyre, Salary, etc.)
        public List<TmbOtherAdvanceVM> OtherAdvances { get; set; } = new();
        public decimal TotalOtherAdvances => OtherAdvances.Sum(x => x.Amount);

        public string NoticeUrdu { get; set; } =
            "نوٹ: - اگر بل میں کسی قسم کی شکایت ہو تو 15 دن کے اندر رجوع کریں۔ اس مدت کے بعد کسی قسم کی شکایت قابل قبول نہیں ہوگی۔";
    }
}