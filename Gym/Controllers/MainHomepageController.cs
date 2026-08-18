using Gym.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Gym.Controllers
{
    public class MainHomepageController : Controller
    {
        private readonly ILogger<MainHomepageController> _logger;

        public MainHomepageController(ILogger<MainHomepageController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
