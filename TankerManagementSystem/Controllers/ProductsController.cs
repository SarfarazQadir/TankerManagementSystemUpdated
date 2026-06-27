using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Models;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _dbcontext;
        public ProductsController(ApplicationDbContext dbcontext) => _dbcontext = dbcontext;
        public IActionResult Index()
        {
            return View(_dbcontext.Products.OrderByDescending(x => x.Id).ToList());
        }
        public IActionResult Add()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Add(Product request)
        {
            bool productExists = _dbcontext.Products
           .Any(x => x.ProductName == request.ProductName);

            if (productExists)
            {
                TempData["Error"] = "Product already exists!";
                return RedirectToAction("Add");
            }

            // Pakistan Time
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
            // Description null handling
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                request.Description = null;
            }

            // Model validation
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Product Name is required";
                return RedirectToAction("Add");
            }

            _dbcontext.Products.Add(request);
            _dbcontext.SaveChanges();
            TempData["add_Product_message"] = "Product add successfully.";

            return RedirectToAction("Index");
        }
        public IActionResult Edit(int id)
        {
            var product = _dbcontext.Products.FirstOrDefault(s => s.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
        [HttpPost]
        public IActionResult Edit(Product updateproduct)
        {
            var product = _dbcontext.Products.FirstOrDefault(s => s.Id == updateproduct.Id);
            if (product == null)
            {
                return NotFound();
            }
            // Optional: Unique name check
            var duplicate = _dbcontext.Products.Any(s => s.ProductName == updateproduct.ProductName && s.Id != updateproduct.Id);
            if (duplicate)
            {
                ModelState.AddModelError("Name", "Product name must be unique.");
                return View(updateproduct);
            }
            // Pakistan Time
            var pakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            product.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pakistanTimeZone);

            product.ProductName = updateproduct.ProductName;
            product.Description = updateproduct.Description;
            // Session se ID lo
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
            product.UpdatedBy = currentUserId;
            _dbcontext.SaveChanges();

            TempData["edit_Product_message"] = "Product edit successfully.";
            return RedirectToAction("Index");
        }
    }
}
