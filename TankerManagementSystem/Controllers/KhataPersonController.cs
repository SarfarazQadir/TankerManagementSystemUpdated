using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Models;

namespace TankerManagementSystem.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AuthorizeModule]
    public class KhataPersonController : Controller
    {
        private readonly ApplicationDbContext _db;
        public KhataPersonController(ApplicationDbContext db) => _db = db;

        public IActionResult Index()
        {
            var data = _db.KhataPersons.OrderBy(x => x.Name).ToList();
            return View(data);
        }

        [HttpGet]
        public IActionResult Add() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(KhataPerson model)
        {
            model.CreatedAt = DateTime.Now;
            model.CreatedBy = User?.Identity?.Name ?? "Admin";
            model.CurrentBalance = 0;

            _db.KhataPersons.Add(model);
            _db.SaveChanges();

            TempData["success"] = "Khata Person Added";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var data = _db.KhataPersons.Find(id);
            if (data == null) return NotFound();
            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(KhataPerson model)
        {
            var data = _db.KhataPersons.Find(model.Id);
            if (data == null) return NotFound();

            data.Name = model.Name;
            data.ContactNumber = model.ContactNumber;
            data.Address = model.Address;
            data.Description = model.Description;
            data.UpdatedAt = DateTime.Now;
            data.UpdatedBy = User?.Identity?.Name ?? "Admin";

            _db.SaveChanges();
            TempData["success"] = "Khata Person Updated";
            return RedirectToAction("Index");
        }

        public IActionResult Delete(int id)
        {
            var data = _db.KhataPersons.Find(id);
            if (data == null) return NotFound();

            bool hasHistory = _db.PersonalKhatas.Any(x => x.KhataPersonId == id);
            if (hasHistory)
            {
                TempData["error"] = "Is person ki ledger history exist karti hai, pehle wo clear karain.";
                return RedirectToAction("Index");
            }

            _db.KhataPersons.Remove(data);
            _db.SaveChanges();
            TempData["success"] = "Deleted";
            return RedirectToAction("Index");
        }

        // Ek person ka full statement dekhne ke liye (PersonalKhataController.Print ki jagah ye use karain)
        public IActionResult Statement(int id)
        {
            var person = _db.KhataPersons.Find(id);
            if (person == null) return NotFound();

            var entries = _db.PersonalKhatas
                .Where(x => x.KhataPersonId == id)
                .OrderBy(x => x.EntryDate)
                .ToList();

            ViewBag.Person = person;
            return View(entries);
        }
    }
}