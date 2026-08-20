using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Gym.Models;
using Gym.Services;
using Microsoft.Extensions.Logging;

namespace Gym.Controllers
{
    [Route("trainer")]
    [Authorize(Roles = "Trainer,Admin,MainAdmin,FrontDesk,Frontdesk")]
    public class TrainerController : Controller
    {
        private readonly ITrainerService _trainerService;
        private readonly ILogger<TrainerController> _logger;

        public TrainerController(ITrainerService trainerService, ILogger<TrainerController> logger)
        {
            _trainerService = trainerService;
            _logger = logger;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Schedule));
        }

        [HttpGet("schedule")]
        public async Task<IActionResult> Schedule([FromQuery] DateTime? date)
        {
            try
            {
                int trainerId = GetCurrentUserId();
                DateTime targetDate = date ?? DateTime.Today;

                TrainerDashboardViewModel viewModel = await _trainerService.GetTrainerDashboardAsync(trainerId, targetDate);

                // Fallback protection: Ensure collections are never null
                viewModel ??= new TrainerDashboardViewModel();
                viewModel.TodaysSessions ??= new List<SessionViewModel>();
                viewModel.WeeklySchedule ??= new Dictionary<string, int>();

                return View("Schedule", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading schedule for trainer {UserId}", GetCurrentUserId());

                // Temporary debug: pass error details or return empty view model instead of hard crash
                var fallbackModel = new TrainerDashboardViewModel
                {
                    TodaysSessions = new List<SessionViewModel>(),
                    WeeklySchedule = new Dictionary<string, int>()
                };

                return View("Schedule", fallbackModel);
            }
        }

        [HttpGet("members")]
        public async Task<IActionResult> Members()
        {
            try
            {
                int trainerId = GetCurrentUserId();
                var clients = await _trainerService.GetTrainerClientsAsync(trainerId);
                return View("Members", clients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading members list for trainer {UserId}", GetCurrentUserId());
                return View("Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> Notifications()
        {
            try
            {
                int trainerId = GetCurrentUserId();
                var notifications = await _trainerService.GetTrainerNotificationsAsync(trainerId);
                return View("Notifications", notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notifications for trainer {UserId}", GetCurrentUserId());
                return View("Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpGet("account")]
        public IActionResult Account()
        {
            return View("Account");
        }

        [HttpPost("session/confirm")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSession(int sessionId)
        {
            try
            {
                await _trainerService.UpdateSessionStatusAsync(sessionId, SessionStatus.Confirmed);
                TempData["SuccessMessage"] = "Session confirmed successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming session {SessionId}", sessionId);
                TempData["ErrorMessage"] = "Failed to confirm session.";
            }

            return RedirectToAction(nameof(Schedule));
        }

        [HttpPost("session/cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSession(int sessionId)
        {
            try
            {
                await _trainerService.UpdateSessionStatusAsync(sessionId, SessionStatus.Cancelled);
                TempData["SuccessMessage"] = "Session cancelled successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling session {SessionId}", sessionId);
                TempData["ErrorMessage"] = "Failed to cancel session.";
            }

            return RedirectToAction(nameof(Schedule));
        }

        #region Helper Methods

        private int GetCurrentUserId()
        {
            // Tries common claims used for user primary keys across authentication setups
            var userIdClaim = User.FindFirst("UserId")
                           ?? User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? User.FindFirst("sub");

            return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
        }

        #endregion
    }
}