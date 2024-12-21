using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShoppingApplication.Controllers
{
    public class SuperAdminController : Controller
    {
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
