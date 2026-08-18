using Gym.Models;
using Gym.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Gym.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly SetupService _setupService;

        public AdminDashboardController(SetupService setupService)
        {
            _setupService = setupService;
        }

        [HttpGet]
        public IActionResult ADashboard()
        {
            return View();
        }

        public IActionResult SetCredentials()
        {
            return View();
        }

        // GET: /AdminDashboard/UserRole
        [HttpGet]
        public async Task<IActionResult> UserRole(int page = 1, int? confirmSuspendId = null, int? confirmDeleteId = null)
        {
            const int pageSize = 10;
            var (users, totalCount) = await _setupService.GetAllUsersAsync(page, pageSize);

            var result = new PagedResult<UserRoleViewModel>
            {
                Items = users,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            // Assign parameters to ViewBag for the confirmation banner display
            ViewBag.ConfirmSuspendId = confirmSuspendId;
            ViewBag.ConfirmDeleteId = confirmDeleteId;

            return View(result);
        }

        // GET: /AdminDashboard/UserActivity/5
        [HttpGet]
        public async Task<IActionResult> UserActivity(int id, int page = 1, int pageSize = 10)
        {
            var users = await _setupService.GetAllUsersAsync();
            var user = users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            ViewBag.User = user;

            var log = await _setupService.GetActivityLogAsync(id) ?? new List<ActivityLogEntry>();

            var pagedResult = new PagedResult<ActivityLogEntry>
            {
                Items = log.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount = log.Count,
                Page = page,
                PageSize = pageSize
            };

            return View(pagedResult);
        }

        // POST: /AdminDashboard/SuspendUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendUser(int id)
        {
            if (!TryBlockSelfAction(id, out var selfActionResult))
            {
                return selfActionResult!;
            }

            var (success, error) = await _setupService.SuspendUserAsync(id);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Account suspended." : error;
            return RedirectToAction(nameof(UserRole));
        }

        // POST: /AdminDashboard/ReactivateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateUser(int id)
        {
            var (success, error) = await _setupService.ReactivateUserAsync(id);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Account reactivated." : error;
            return RedirectToAction(nameof(UserRole));
        }

        // POST: /AdminDashboard/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!TryBlockSelfAction(id, out var selfActionResult))
            {
                return selfActionResult!;
            }

            var (success, error) = await _setupService.DeleteUserAsync(id);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Account deleted." : error;
            return RedirectToAction(nameof(UserRole));
        }

        /// <summary>
        /// Stops an admin from suspending/deleting their own logged-in account.
        /// </summary>
        private bool TryBlockSelfAction(int targetId, out IActionResult? result)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(currentUserId, out int myId) && myId == targetId)
            {
                TempData["ErrorMessage"] = "You can't perform this action on your own account.";
                result = RedirectToAction(nameof(UserRole));
                return false;
            }

            result = null;
            return true;
        }
    }
}