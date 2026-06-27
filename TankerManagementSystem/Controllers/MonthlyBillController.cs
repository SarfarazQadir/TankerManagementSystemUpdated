using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Models;

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
        /*    public IActionResult Generate(int tankerId, int month, int year)
            {
                var tanker = _db.Tankers
                    .Include(x => x.Owner)
                    .FirstOrDefault(x => x.Id == tankerId);

                if (tanker == null)
                {
                    return NotFound();
                }

                var ledgers = _db.TripLedgers

                    .Include(x => x.Product)

                    .Include(x => x.Expenses)

                    .Include(x => x.TripEntryFk)
                    .ThenInclude(x => x.TankerFk)

                    .Where(x =>

                        x.TripEntryFk.TankerId == tankerId
                        &&
                        x.TripDate.Month == month
                        &&
                        x.TripDate.Year == year

                    )

                    .OrderBy(x => x.TripDate)

                    .ToList();

                ViewBag.Tanker = tanker;
                ViewBag.Month = month;
                ViewBag.Year = year;

                return View(ledgers);
            }
        */

        public IActionResult Generate(int tankerId, int month, int year)
        {
            var tanker = _db.Tankers
                .Include(x => x.Owner)
                .FirstOrDefault(x => x.Id == tankerId);

            if (tanker == null)
            {
                return NotFound();
            }

            var ledgers = _db.TripLedgers
                .Include(x => x.Product)
                .Include(x => x.Expenses)
                .Include(x => x.TripEntryFk)
                .ThenInclude(x => x.TankerFk)
                .Where(x =>
                    x.TripEntryFk.TankerId == tankerId &&
                    x.TripDate.Month == month &&
                    x.TripDate.Year == year)
                .OrderBy(x => x.TripDate)
                .ToList();

            decimal totalFreight = ledgers.Sum(x => x.Freight);

            decimal totalDebit =
                ledgers.Sum(x => x.AdvanceCash) +
                ledgers.Sum(x => x.Shortage) +
                ledgers.Sum(x => x.Commission) +
                ledgers.Sum(x => x.Munshiana) +
                ledgers.Sum(x => x.Expenses.Sum(e => e.Amount));

            decimal totalPaid =
                ledgers.Sum(x => x.AmountPay ?? 0);

            decimal monthNet =
                totalFreight -
                totalDebit -
                totalPaid;

            decimal openingBalance =
                tanker.CurrentBalance - monthNet;

            ViewBag.OpeningBalance = openingBalance;
            ViewBag.ClosingBalance = tanker.CurrentBalance;

            ViewBag.Tanker = tanker;
            ViewBag.Month = month;
            ViewBag.Year = year;

            return View(ledgers);
        }

    }
}