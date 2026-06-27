using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TankerManagementSystem.Models;

namespace TankerManagementSystem.Controllers.Api
{
    //[Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _mycon;
        public AdminApiController(ApplicationDbContext mycon)
        {
            _mycon = mycon;
        }
        //[Authorize]
        [HttpGet("GetDashboardData")]
        public IActionResult GetDashboardData()
        {
            /*var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(currentUserId))
            {
                return RedirectToAction("Login", "Admin");
            }

            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized(new { message = "Session expired. Please login again." });
            }*/

            DateTime today = DateTime.Today;
            DateTime weekStart = today.AddDays(-(int)today.DayOfWeek);
            DateTime monthStart = new DateTime(today.Year, today.Month, 1);
            DateTime yearStart = new DateTime(today.Year, 1, 1);

            // TOTAL COUNTS
            int totalTrips = _mycon.TripLedgers.Count();
            int totalTankers = _mycon.Tankers.Count();
            int totalOwners = _mycon.TankerOwners.Count();

            // PROFITS & EXPENSES
            decimal totalProfit = _mycon.TripLedgers.Sum(x => (decimal?)x.Commission) ?? 0;
            decimal todayProfit = _mycon.TripLedgers.Where(x => x.CreatedAt.Date == today).Sum(x => (decimal?)x.Commission) ?? 0;
            decimal weekProfit = _mycon.TripLedgers.Where(x => x.CreatedAt >= weekStart).Sum(x => (decimal?)x.Commission) ?? 0;
            decimal monthProfit = _mycon.TripLedgers.Where(x => x.CreatedAt >= monthStart).Sum(x => (decimal?)x.Commission) ?? 0;
            decimal yearProfit = _mycon.TripLedgers.Where(x => x.CreatedAt >= yearStart).Sum(x => (decimal?)x.Commission) ?? 0;
            decimal totalExpenses = _mycon.TripExpenses.Sum(x => (decimal?)x.Amount) ?? 0;
            decimal netProfit = totalProfit - totalExpenses;

            // 1. MONTHLY DATA (For Bar Chart)
            var monthlyStats = _mycon.TripLedgers
                .Where(x => x.CreatedAt.Year == today.Year)
                .GroupBy(x => x.CreatedAt.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Profit = g.Sum(x => (decimal?)x.Commission) ?? 0
                })
                .OrderBy(x => x.Month)
                .ToList();

            var monthsList = new List<string>();
            var revenueList = new List<decimal>();

            for (int i = 1; i <= 12; i++)
            {
                monthsList.Add(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(i));
                var stat = monthlyStats.FirstOrDefault(x => x.Month == i);
                revenueList.Add(stat != null ? Math.Floor(stat.Profit) : 0);
            }

            // 2. YEARLY DATA (For Line Chart)
            var yearlyStats = _mycon.TripLedgers
                .GroupBy(x => x.CreatedAt.Year)
                .Select(g => new
                {
                    Year = g.Key,
                    Profit = g.Sum(x => (decimal?)x.Commission) ?? 0
                })
                .OrderBy(x => x.Year)
                .ToList();

            var yearsList = yearlyStats.Select(x => x.Year.ToString()).ToList();
            var yearlyRevenueList = yearlyStats.Select(x => Math.Floor(x.Profit)).ToList();

            return Ok(new
            {
                months = monthsList,
                revenue = revenueList,
                years = yearsList,
                yearlyRevenue = yearlyRevenueList,
                totalTrips,
                totalTankers,
                totalOwners,
                totalProfit,
                todayProfit,
                weekProfit,
                monthProfit,
                yearProfit,
                totalExpenses,
                netProfit
            });
        }
        [HttpGet("employees")]
        public IEnumerable<string> Get()
        {
            return new List<string> { "Ahmed", "Ali", "Ahsan" };
        }
    }
}
