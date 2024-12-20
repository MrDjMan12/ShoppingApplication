using Microsoft.AspNetCore.Mvc;

namespace ShoppingApplication.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
