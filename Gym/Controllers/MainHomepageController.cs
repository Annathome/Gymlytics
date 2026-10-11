using Gym.Models;
using Gym.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Gym.Controllers
{
    public class MainHomepageController : Controller
    {
        private readonly ILogger<MainHomepageController> _logger;
        private readonly GymDbContext _context;

        public MainHomepageController(ILogger<MainHomepageController> logger, GymDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var nextMonth = monthStart.AddMonths(1);
            ViewBag.ActiveMembers = await _context.Clients.AsNoTracking()
                .CountAsync(item => item.IsActive && item.IsActiveMember);
            ViewBag.ActiveTrainers = await _context.Trainers.AsNoTracking()
                .CountAsync(item => item.IsActive);
            ViewBag.MonthlyVisits =
                await _context.WalkInClients.AsNoTracking().CountAsync(item => item.RegisteredAt >= monthStart && item.RegisteredAt < nextMonth)
                + await _context.MemberAttendanceLogs.AsNoTracking().CountAsync(item => item.CheckInAt >= monthStart && item.CheckInAt < nextMonth);
            ViewBag.MembershipPlans = MembershipPlanPricing.GetActivePlans();
            ViewBag.WalkInPaymentTypes = await _context.WalkInPaymentTypes
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.Name)
                .ToListAsync();
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

        public IActionResult Services()
        {
            return RedirectToAction("Index", "MainHomepage", null, "services");
        }

        public IActionResult Pricing()
        {
            return RedirectToAction("Index", "MainHomepage", null, "pricing");
        }
    }
}
