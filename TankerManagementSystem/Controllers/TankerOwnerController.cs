using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TankerManagementSystem.Models;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Admin")]
    public class TankerOwnerController : Controller
    {
        private readonly ApplicationDbContext _dbcontext;

        public TankerOwnerController(ApplicationDbContext dbcontext)
        {
            _dbcontext = dbcontext;
        }

        // LIST
        public IActionResult Index()
        {
            var owners = _dbcontext.TankerOwners
                .OrderByDescending(x => x.Id)
                .ToList();

            return View(owners);
        }

        // ADD GET
        public IActionResult Add()
        {
            return View();
        }

        // ADD POST
        [HttpPost]
        public IActionResult Add(TankerOwner request)
        {
            bool tankerOwner = _dbcontext.TankerOwners
            .Any(x => x.Name == request.Name);

            if (tankerOwner)
            {
                TempData["Error"] = "Tanker Owner already exists!";
                return RedirectToAction("Add");
            }
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

            // Basic validation
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                TempData["Error"] = "Owner Name is required";
                return RedirectToAction("Add");
            }

            _dbcontext.TankerOwners.Add(request);
            _dbcontext.SaveChanges();

            TempData["add_owner_message"] = "Tanker Owner added successfully.";
            return RedirectToAction("Index");
        }

        // EDIT GET
        public IActionResult Edit(int id)
        {
            var owner = _dbcontext.TankerOwners.FirstOrDefault(x => x.Id == id);

            if (owner == null)
                return NotFound();

            return View(owner);
        }

        // EDIT POST
        [HttpPost]
        public IActionResult Edit(TankerOwner updateOwner)
        {
            var owner = _dbcontext.TankerOwners.FirstOrDefault(x => x.Id == updateOwner.Id);

            if (owner == null)
                return NotFound();

            // Optional: Duplicate CNIC check
            var duplicate = _dbcontext.TankerOwners
                .Any(x => x.CNIC == updateOwner.CNIC && x.Id != updateOwner.Id);

            if (duplicate)
            {
                ModelState.AddModelError("CNIC", "CNIC must be unique.");
                return View(updateOwner);
            }

            var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            owner.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);

            owner.Name = updateOwner.Name;
            owner.Phone = updateOwner.Phone;
            owner.CNIC = updateOwner.CNIC;
            owner.Address = updateOwner.Address;

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
            owner.UpdatedBy = currentUserId;

            _dbcontext.SaveChanges();

            TempData["edit_owner_message"] = "Tanker Owner updated successfully.";
            return RedirectToAction("Index");
        }
    }
}