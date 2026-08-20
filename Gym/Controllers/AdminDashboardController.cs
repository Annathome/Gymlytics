using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Gym.Models;
using Gym.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gym.Controllers
{
    /// <summary>
    /// Admin Controller - Handles UI routes for admin portal management
    /// </summary>
    [Authorize(Roles = "Admin,MainAdmin")]
    [Route("Admin/[action]")]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly SetupService _setupService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IAdminService adminService,
            SetupService setupService,
            ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _setupService = setupService;
            _logger = logger;
        }

        [HttpGet("/Admin")]
        [HttpGet]
        public async Task<IActionResult> ADashboard()
        {
            try
            {
                var statistics = await _adminService.GetDashboardStatisticsAsync();
                return View("~/Views/AdminDashboard/ADashboard.cshtml", statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                ViewBag.ErrorMessage = "Failed to load dashboard data.";
                return View("~/Views/AdminDashboard/ADashboard.cshtml");
            }
        }

        // GET: /Admin/Members

        [HttpGet("Members")]
        public async Task<IActionResult> Members()
        {
            try
            {
                var clients = await _adminService.GetAllClientsAsync();
                return View(clients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving members list");
                TempData["ErrorMessage"] = "Could not retrieve members list.";
                return View(new List<Client>());
            }
        }

        // GET: /Admin/Trainers
        [HttpGet("Trainers")]
        public async Task<IActionResult> Trainers()
        {
            try
            {
                var trainers = await _adminService.GetAllTrainersAsync();
                return View(trainers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trainers list");
                TempData["ErrorMessage"] = "Could not retrieve trainers list.";
                return View(new List<Trainer>());
            }
        }

        // GET: /Admin/Payments

        [HttpGet("Payments")]
        public async Task<IActionResult> Payments(string status = null)
        {
            try
            {
                PaymentStatus? paymentStatus = null;
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<PaymentStatus>(status, out var parsedStatus))
                {
                    paymentStatus = parsedStatus;
                }

                ViewBag.CurrentStatus = status;
                var payments = await _adminService.GetPaymentsAsync(paymentStatus);
                return View(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments list");
                TempData["ErrorMessage"] = "Could not load payments.";
                return View(new List<Payment>());
            }
        }

        // GET: /Admin/ExpiringMemberships
        [HttpGet("ExpiringMemberships")]
        public async Task<IActionResult> ExpiringMemberships(int days = 7)
        {
            try
            {
                ViewBag.Days = days;
                var memberships = await _adminService.GetExpiringMembershipsAsync(days);
                return View(memberships);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expiring memberships");
                TempData["ErrorMessage"] = "Could not load expiring memberships.";
                return View();
            }
        }

        // GET: /Admin/Equipment
        [HttpGet("Equipment")]
        public async Task<IActionResult> Equipment()
        {
            try
            {
                var equipmentList = await _adminService.GetEquipmentStatusAsync();
                return View(equipmentList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading equipment status");
                TempData["ErrorMessage"] = "Failed to load equipment list.";
                return View(new List<Equipment>());
            }
        }

        // POST: /Admin/UpdateEquipmentStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEquipmentStatus(int equipmentId, string status)
        {
            try
            {
                var success = await _adminService.UpdateEquipmentStatusAsync(equipmentId, status);
                if (success)
                {
                    TempData["SuccessMessage"] = "Equipment status updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Equipment item not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating equipment status");
                TempData["ErrorMessage"] = "An error occurred while updating status.";
            }

            return RedirectToAction(nameof(Equipment));
        }

        // GET: /Admin/UserRoles
        [HttpGet("UserRoles")]
        public async Task<IActionResult> UserRoles(int page = 1, int? confirmSuspendId = null, int? confirmDeleteId = null)
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

            ViewBag.ConfirmSuspendId = confirmSuspendId;
            ViewBag.ConfirmDeleteId = confirmDeleteId;

            return View("~/Views/AdminDashboard/UserRoles.cshtml", result);
        }

        // POST: /Admin/SuspendUser
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
            return RedirectToAction(nameof(UserRoles));
        }

        // POST: /Admin/ReactivateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateUser(int id)
        {
            var (success, error) = await _setupService.ReactivateUserAsync(id);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Account reactivated." : error;
            return RedirectToAction(nameof(UserRoles));
        }

        // POST: /Admin/DeleteUser
        [HttpPost("DeleteUser")]
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
            return RedirectToAction(nameof(UserRoles));
        }

        /// <summary>
        /// Prevents an admin from performing harmful operations on their active account
        /// </summary>
        private bool TryBlockSelfAction(int targetId, out IActionResult? result)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("UserId")?.Value;

            if (int.TryParse(currentUserId, out int myId) && myId == targetId)
            {
                TempData["ErrorMessage"] = "You cannot perform this action on your own account.";
                result = RedirectToAction(nameof(UserRoles));
                return false;
            }

            result = null;
            return true;
        }
    }
}