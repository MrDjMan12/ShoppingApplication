using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShoppingApplication.Controllers
{
    [Authorize(Policy = "SuperAdminOnly")]
    public class SuperAdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
