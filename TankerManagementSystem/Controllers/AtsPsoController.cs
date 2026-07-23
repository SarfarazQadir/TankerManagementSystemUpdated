// Modified by AI
// Date: 2026-07-21
// Reason: H-04 — Added [Authorize] attributes. This controller had NO authorization
// attributes, making ALL its actions (Add, Edit, Delete, AllReport) publicly accessible
// to unauthenticated users. This could allow anyone on the network to create/modify
// ATS/PSO entries and corrupt tanker ledger balances.
// H-05 — Added ILedgerRecalculationService injection to replace duplicated private method.
using System;
using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Helpers;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.ViewModels;
using TankerManagementSystem.Services;

namespace TankerManagementSystem.Controllers
{
// Modified by AI
// Date: 2026-07-21
// Reason: H-04 — Added [Authorize] + [AuthorizeModule] attributes.
// Previously this controller had no authorization at all — all actions were public.
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[AuthorizeModule]
public class AtsPsoController : Controller
{
    private readonly ApplicationDbContext _db;

    // Modified by AI
    // Date: 2026-07-21
    // Reason: H-05 — Injecting ILedgerRecalculationService replaces the private
    // RecalculateTankerLedger() method that was duplicated in 4 controllers.
    private readonly ILedgerRecalculationService _recalcService;

    public AtsPsoController(ApplicationDbContext db, ILedgerRecalculationService recalcService)
    {
        _db = db;
        _recalcService = recalcService;
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
                    var pakTime = DateTimeHelper.GetPakistanTime();

                    var tanker = _db.Tankers.FirstOrDefault(t => t.Id == tankerId);
                    if (tanker == null) return NotFound();

                    // 1. Create ledger entry (RunningBalance will be set by RecalculateTankerLedger)
                    var ledger = new TankerLedger
                    {
                        TankerId = tankerId,
                        TransactionDate = entryDate,
                        ModuleName = "ATS PSO",
                        Credit = 0,
                        Debit = amount,
                        RunningBalance = 0, // temporary — recalculation will set this
                        Description = description,
                        CreatedAt = pakTime,
                        CreatedBy = User.Identity?.Name ?? "System"
                    };
                    _db.TankerLedgers.Add(ledger);
                    _db.SaveChanges();

                    // 2. ATS Entry create with LedgerId mapped
                    var atsEntry = new AtsPsoEntry
                    {
                        TankerId = tankerId,
                        Amount = amount,
                        Description = description,
                        EntryDate = entryDate,
                        TankerLedgerId = ledger.Id,
                        CreatedBy = User.Identity?.Name ?? "System",
                        CreatedAt = pakTime
                    };
                    _db.AtsPsoEntries.Add(atsEntry);
                    _db.SaveChanges();

                    // 3. Map ATS Entry primary key back to Ledger ReferenceId
                    ledger.ReferenceId = atsEntry.Id;
                    _db.TankerLedgers.Update(ledger);
                    _db.SaveChanges();

                    // FIX Issue 04: Recalculate the entire tanker ledger chain - Done by AntiGravity on 2026-07-18 08:15 PST
                    _recalcService.RecalculateTankerLedger(tankerId);

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
                    var pakTime = DateTimeHelper.GetPakistanTime();

                    var oldEntry = _db.AtsPsoEntries.AsNoTracking().FirstOrDefault(e => e.Id == model.Id);
                    if (oldEntry == null) return NotFound();

                    // FIX Issue 01: Correctly handle tanker switching - Done by AntiGravity on 2026-07-18 08:15 PST
                    int oldTankerId = oldEntry.TankerId;
                    int newTankerId = model.TankerId;

                    // Update the tanker ledger entry
                    var ledger = _db.TankerLedgers.FirstOrDefault(l => l.Id == oldEntry.TankerLedgerId);
                    if (ledger != null)
                    {
                        ledger.TankerId = newTankerId;
                        ledger.TransactionDate = model.EntryDate;
                        ledger.Debit = model.Amount;
                        ledger.Description = model.Description;
                        ledger.ReferenceId = model.Id;
                        // RunningBalance will be set by RecalculateTankerLedger
                        _db.TankerLedgers.Update(ledger);
                    }

                    model.UpdatedAt = pakTime;
                    model.UpdatedBy = User.Identity?.Name ?? "System";
                    model.CreatedAt = oldEntry.CreatedAt;
                    model.CreatedBy = oldEntry.CreatedBy;
                    model.TankerLedgerId = oldEntry.TankerLedgerId;

                    _db.AtsPsoEntries.Update(model);
                    _db.SaveChanges();

                    // Modified by AI
                    // Date: 2026-07-21
                    // Reason: H-05 — Using service instead of private method.
                    // FIX Issue 01 + 04: Recalculate both old and new tanker ledger chains
                    _recalcService.RecalculateTankerLedger(oldTankerId);
                    if (newTankerId != oldTankerId)
                    {
                        _recalcService.RecalculateTankerLedger(newTankerId);
                    }

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