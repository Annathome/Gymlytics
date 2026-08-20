using Gym.Models;
using Gym.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Gym.Controllers.Api
{
    /// <summary>
    /// RESTful API Controller for Trainer Dashboard, Schedule, Clients, and Notifications.
    /// </summary>
    [Authorize(Roles = "Trainer")]
    [ApiController]
    [Route("api/trainer")]
    [Produces("application/json")]
    public class TrainerApiController : ControllerBase
    {
        private readonly ITrainerService _trainerService;
        private readonly ILogger<TrainerApiController> _logger;

        public TrainerApiController(ITrainerService trainerService, ILogger<TrainerApiController> logger)
        {
            _trainerService = trainerService;
            _logger = logger;
        }

        /// <summary>
        /// GET: api/trainer/dashboard
        /// Returns dashboard metrics, upcoming sessions, and recent client activity.
        /// </summary>
        [HttpGet("dashboard")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TrainerDashboardViewModel))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                int trainerId = GetCurrentUserId();
                var dashboardData = await _trainerService.GetTrainerDashboardAsync(trainerId);

                if (dashboardData == null)
                {
                    return Ok(new TrainerDashboardViewModel
                    {
                        TrainerId = trainerId,
                        TrainerName = User.Identity?.Name ?? "Trainer"
                    });
                }

                return Ok(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Error retrieving dashboard for Trainer ID: {UserId}", GetCurrentUserId());
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred fetching dashboard data." });
            }
        }

        /// <summary>
        /// GET: api/trainer/schedule?date=2026-08-19
        /// Retrieves training sessions for a given date (defaults to today).
        /// </summary>
        [HttpGet("schedule")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<SessionViewModel>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSchedule([FromQuery] DateTime? date)
        {
            try
            {
                int trainerId = GetCurrentUserId();
                DateTime targetDate = date ?? DateTime.Today;

                var sessions = await _trainerService.GetTrainerSessionsAsync(trainerId, targetDate);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Error retrieving schedule for Trainer ID: {UserId}", GetCurrentUserId());
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred fetching schedule data." });
            }
        }

        /// <summary>
        /// GET: api/trainer/members
        /// Retrieves the list of clients assigned to the trainer.
        /// </summary>
        [HttpGet("members")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<ClientViewModel>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMembers()
        {
            try
            {
                int trainerId = GetCurrentUserId();
                var clients = await _trainerService.GetTrainerClientsAsync(trainerId);
                return Ok(clients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Error retrieving members for Trainer ID: {UserId}", GetCurrentUserId());
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred fetching members list." });
            }
        }

        /// <summary>
        /// GET: api/trainer/notifications
        /// Retrieves unread and recent notifications for the trainer.
        /// </summary>
        [HttpGet("notifications")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<NotificationViewModel>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                int trainerId = GetCurrentUserId();
                var notifications = await _trainerService.GetTrainerNotificationsAsync(trainerId);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Error retrieving notifications for Trainer ID: {UserId}", GetCurrentUserId());
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred fetching notifications." });
            }
        }

        /// <summary>
        /// PATCH/PUT: api/trainer/sessions/{sessionId}/status
        /// Updates the status of a specific session (e.g., Confirmed, Cancelled, Completed).
        /// </summary>
        [HttpPatch("sessions/{sessionId:int}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateSessionStatus(int sessionId, [FromBody] UpdateSessionStatusRequest request)
        {
            if (request == null || !Enum.IsDefined(typeof(SessionStatus), request.Status))
            {
                return BadRequest(new { message = "Invalid status value supplied." });
            }

            try
            {
                bool success = await _trainerService.UpdateSessionStatusAsync(sessionId, request.Status);

                if (!success)
                {
                    return BadRequest(new { message = "Unable to update session status or session not found." });
                }

                return Ok(new { message = "Session status updated successfully.", sessionId, newStatus = request.Status.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Error updating session {SessionId} status", sessionId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred updating the session." });
            }
        }

        #region Helper Methods

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
        }

        #endregion
    }

    /// <summary>
    /// DTO for updating session status via API body
    /// </summary>
    public class UpdateSessionStatusRequest
    {
        public SessionStatus Status { get; set; }
    }
}