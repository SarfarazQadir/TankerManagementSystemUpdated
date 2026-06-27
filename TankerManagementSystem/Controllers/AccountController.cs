using Microsoft.AspNetCore.Mvc;

namespace TankerManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        [Route("Account/AccessDenied")]
        public IActionResult AccessDenied(string message)
        {
            // Assigns professional fallback message if none is provided dynamically
            ViewBag.ErrorMessage = message ?? "You do not have the required permissions to access this resource.";
            return View();
        }
    }
}