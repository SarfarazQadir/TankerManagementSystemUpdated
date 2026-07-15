using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.ViewModels;

namespace TankerManagementSystem.Controllers
{
    public class AtsPsoController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AtsPsoController(ApplicationDbContext db)
        {
            _db = db;
        }

        // 1. INDEX: List View with Date Filtering Support
        public IActionResult Index(DateTime? startDate, DateTime? endDate)
        {
            var query = _db.AtsPsoEntries
                .Include(e => e.Tanker)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(x => x.EntryDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(x => x.EntryDate <= endDate.Value.Date);
            }

            var historyList = query
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt)
                .ToList();

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(historyList);
        }

        // 2. GET: Create View
        public IActionResult Create()
        {
            PopulateTankersDropdown();
            return View();
        }

        // 3. POST: Create Entry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(int tankerId, decimal amount, string description, DateTime entryDate)
        {
            if (tankerId <= 0 || amount <= 0 || string.IsNullOrEmpty(description) || entryDate == DateTime.MinValue)
            {
                TempData["Error"] = "Please fill all the required fields.";
                PopulateTankersDropdown();
                return View();
            }

            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var tanker = _db.Tankers.FirstOrDefault(t => t.Id == tankerId);
                    if (tanker == null) return NotFound();

                    tanker.CurrentBalance -= amount;

                    // 1. Initial ledger create karein
                    var ledger = new TankerLedger
                    {
                        TankerId = tankerId,
                        TransactionDate = entryDate,
                        ModuleName = "ATS PSO",
                        Credit = 0,
                        Debit = amount,
                        RunningBalance = tanker.CurrentBalance,
                        Description = description,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name ?? "System"
                    };
                    _db.TankerLedgers.Add(ledger);
                    _db.SaveChanges();

                    // 2. ATS Entry create karein jisme LedgerId map ho
                    var atsEntry = new AtsPsoEntry
                    {
                        TankerId = tankerId,
                        Amount = amount,
                        Description = description,
                        EntryDate = entryDate,
                        TankerLedgerId = ledger.Id,
                        CreatedBy = User.Identity?.Name ?? "System",
                        CreatedAt = DateTime.Now
                    };
                    _db.AtsPsoEntries.Add(atsEntry);
                    _db.SaveChanges();

                    // 3. CRITICAL FIX: Ledger ki ReferenceId me ATS Entry ki primary key map karein
                    ledger.ReferenceId = atsEntry.Id;
                    _db.TankerLedgers.Update(ledger);
                    _db.SaveChanges();

                    transaction.Commit();
                    TempData["Success"] = "ATS PSO entry added successfully!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Error: " + ex.Message;
                }
            }
            PopulateTankersDropdown();
            return View();
        }
        // 4. GET: Edit View
        public IActionResult Edit(int id)
        {
            var entry = _db.AtsPsoEntries.FirstOrDefault(e => e.Id == id);
            if (entry == null) return NotFound();

            PopulateTankersDropdown();
            return View(entry);
        }

        // 5. POST: Update Entry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(AtsPsoEntry model)
        {
            if (model.TankerId <= 0 || model.Amount <= 0 || string.IsNullOrEmpty(model.Description))
            {
                TempData["Error"] = "Please fill all the required fields.";
                PopulateTankersDropdown();
                return View(model);
            }

            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var oldEntry = _db.AtsPsoEntries.AsNoTracking().FirstOrDefault(e => e.Id == model.Id);
                    if (oldEntry == null) return NotFound();

                    var tanker = _db.Tankers.FirstOrDefault(t => t.Id == model.TankerId);
                    if (tanker == null) return NotFound();

                    tanker.CurrentBalance += oldEntry.Amount;
                    tanker.CurrentBalance -= model.Amount;

                    var ledger = _db.TankerLedgers.FirstOrDefault(l => l.Id == oldEntry.TankerLedgerId);
                    if (ledger != null)
                    {
                        ledger.TankerId = model.TankerId;
                        ledger.TransactionDate = model.EntryDate;
                        ledger.Debit = model.Amount;
                        ledger.RunningBalance = tanker.CurrentBalance;
                        ledger.Description = model.Description;
                        ledger.ReferenceId = model.Id; // Syncing Reference ID on edit too
                        _db.TankerLedgers.Update(ledger);
                    }

                    model.UpdatedAt = DateTime.Now;
                    model.UpdatedBy = User.Identity?.Name ?? "System";
                    model.CreatedAt = oldEntry.CreatedAt;
                    model.CreatedBy = oldEntry.CreatedBy;
                    model.TankerLedgerId = oldEntry.TankerLedgerId;

                    _db.AtsPsoEntries.Update(model);
                    _db.SaveChanges();

                    transaction.Commit();
                    TempData["Success"] = "Record update successful!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Error: " + ex.Message;
                }
            }
            PopulateTankersDropdown();
            return View(model);
        }
        // 6. GET: AllReport (Dedicated Print Page)
        public IActionResult AllReport(DateTime? startDate, DateTime? endDate)
        {
            var query = _db.AtsPsoEntries
                .Include(e => e.Tanker)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(x => x.EntryDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(x => x.EntryDate <= endDate.Value.Date);
            }

            // ViewModel mapping with included Tanker Info
            var data = query
                .OrderByDescending(x => x.EntryDate)
                .ThenByDescending(x => x.CreatedAt)
                .Select(x => new AtsPsoReportVM
                {
                    Id = x.Id,
                    EntryDate = x.EntryDate,
                    TankerNo = x.Tanker != null ? x.Tanker.TankerNo : "N/A",
                    Capacity = x.Tanker != null ? x.Tanker.Capacity : "N/A",
                    Description = x.Description,
                    Amount = x.Amount,
                    TankerRunningBalance = x.Tanker != null ? x.Tanker.CurrentBalance : 0,
                    CreatedByName = x.CreatedBy ?? "System"
                })
                .ToList();

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(data);
        }

        private void PopulateTankersDropdown()
        {
            ViewBag.Tankers = _db.Tankers
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = $"{t.TankerNo} ({t.Capacity})"
                }).ToList();
        }
    }
}