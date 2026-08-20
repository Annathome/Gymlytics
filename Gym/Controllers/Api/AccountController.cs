using System;
using System.Collections.Generic;
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
    /// Auth API Controller - Handles authentication and JWT token generation
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly JwtTokenService _jwtTokenService;
        private readonly SetupService _setupService;

        public AuthApiController(JwtTokenService jwtTokenService, SetupService setupService)
        {
            _jwtTokenService = jwtTokenService;
            _setupService = setupService;
        }

        /// <summary>
        /// POST: api/auth/login
        /// Validates credentials and returns JWT bearer token
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            var (isValid, userRole, userId, username, email, phone, errorMessage) =
                await _setupService.ValidateUserAsync(model.Username, model.Password);

            if (!isValid)
            {
                return Unauthorized(new { message = errorMessage ?? "Invalid credentials." });
            }

            // Generate the JWT token string
            var token = _jwtTokenService.GenerateToken(userId, username ?? model.Username, userRole);

            return Ok(new
            {
                message = "Login successful",
                token = token,
                userId,
                username = username ?? model.Username,
                role = userRole,
                email,
                phone
            });
        }
    }

    /// <summary>
    /// Admin Controller - Handles administrative API endpoints for reporting, user management, and gym operations
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly SetupService _setupService;
        private readonly ILogger<AdminApiController> _logger;

        public AdminApiController(
            IAdminService adminService,
            SetupService setupService,
            ILogger<AdminApiController> logger)
        {
            _adminService = adminService;
            _setupService = setupService;
            _logger = logger;
        }

        /// <summary>
        /// GET: api/adminapi/dashboard
        /// Retrieves high-level dashboard metrics
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult<DashboardStatistics>> GetDashboard()
        {
            try
            {
                var statistics = await _adminService.GetDashboardStatisticsAsync();
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving admin dashboard statistics");
                return StatusCode(500, "An error occurred while retrieving dashboard statistics");
            }
        }

        /// <summary>
        /// GET: api/adminapi/members
        /// Retrieves all gym members
        /// </summary>
        [HttpGet("members")]
        public async Task<ActionResult<List<Client>>> GetMembers()
        {
            try
            {
                var clients = await _adminService.GetAllClientsAsync();
                return Ok(clients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving gym members");
                return StatusCode(500, "An error occurred while retrieving members");
            }
        }

        /// <summary>
        /// GET: api/adminapi/trainers
        /// Retrieves all gym trainers
        /// </summary>
        [HttpGet("trainers")]
        public async Task<ActionResult<List<Trainer>>> GetTrainers()
        {
            try
            {
                var trainers = await _adminService.GetAllTrainersAsync();
                return Ok(trainers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trainers");
                return StatusCode(500, "An error occurred while retrieving trainers");
            }
        }

        /// <summary>
        /// GET: api/adminapi/payments?status=Pending
        /// Retrieves gym payments, optionally filtered by status
        /// </summary>
        [HttpGet("payments")]
        public async Task<ActionResult<List<Payment>>> GetPayments([FromQuery] string? status = null)
        {
            try
            {
                PaymentStatus? paymentStatus = null;
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<PaymentStatus>(status, true, out var ps))
                {
                    paymentStatus = ps;
                }

                var payments = await _adminService.GetPaymentsAsync(paymentStatus);
                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments");
                return StatusCode(500, "An error occurred while retrieving payments");
            }
        }

        /// <summary>
        /// GET: api/adminapi/payments/recent?count=10
        /// Retrieves recent payment logs
        /// </summary>
        [HttpGet("payments/recent")]
        public async Task<ActionResult<List<RecentPayment>>> GetRecentPayments([FromQuery] int count = 10)
        {
            try
            {
                var payments = await _adminService.GetRecentPaymentsAsync(count);
                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent payments");
                return StatusCode(500, "An error occurred while retrieving recent payments");
            }
        }

        /// <summary>
        /// GET: api/adminapi/memberships/expiring?days=7
        /// Retrieves memberships approaching expiration
        /// </summary>
        [HttpGet("memberships/expiring")]
        public async Task<ActionResult<List<ExpiringMembership>>> GetExpiringMemberships([FromQuery] int days = 7)
        {
            try
            {
                var memberships = await _adminService.GetExpiringMembershipsAsync(days);
                return Ok(memberships);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expiring memberships");
                return StatusCode(500, "An error occurred while retrieving expiring memberships");
            }
        }

        /// <summary>
        /// GET: api/adminapi/equipment
        /// Retrieves equipment inventory and status
        /// </summary>
        [HttpGet("equipment")]
        public async Task<ActionResult<List<Equipment>>> GetEquipment()
        {
            try
            {
                var equipment = await _adminService.GetEquipmentStatusAsync();
                return Ok(equipment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving equipment status");
                return StatusCode(500, "An error occurred while retrieving equipment");
            }
        }

        /// <summary>
        /// PUT: api/adminapi/equipment/{equipmentId}/status
        /// Updates operational status for a piece of equipment
        /// </summary>
        [HttpPut("equipment/{equipmentId}/status")]
        public async Task<IActionResult> UpdateEquipmentStatus(int equipmentId, [FromBody] UpdateEquipmentRequest request)
        {
            try
            {
                var success = await _adminService.UpdateEquipmentStatusAsync(equipmentId, request.Status);
                if (!success)
                    return NotFound(new { message = "Equipment not found" });

                return Ok(new { message = "Equipment status updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating equipment status");
                return StatusCode(500, "An error occurred while updating equipment status");
            }
        }

        /// <summary>
        /// GET: api/adminapi/revenue?startDate=2026-08-01&endDate=2026-08-31
        /// Calculates revenue within a date window
        /// </summary>
        [HttpGet("revenue")]
        public async Task<ActionResult<object>> GetRevenue([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            try
            {
                if (startDate > endDate)
                    return BadRequest("Start date must be before end date");

                var revenue = await _adminService.GetTotalRevenueAsync(startDate, endDate);
                return Ok(new { revenue });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating revenue");
                return StatusCode(500, "An error occurred while calculating revenue");
            }
        }

        /// <summary>
        /// GET: api/adminapi/users?page=1&pageSize=10
        /// Retrieves paginated system users
        /// </summary>
        [HttpGet("users")]
        public async Task<ActionResult<PagedResult<UserRoleViewModel>>> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var (users, totalCount) = await _setupService.GetAllUsersAsync(page, pageSize);
                return Ok(new PagedResult<UserRoleViewModel>
                {
                    Items = users,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, "An error occurred while retrieving users");
            }
        }

        /// <summary>
        /// POST: api/adminapi/users/{id}/suspend
        /// Suspends a user account
        /// </summary>
        [HttpPost("users/{id}/suspend")]
        public async Task<IActionResult> SuspendUser(int id)
        {
            if (IsSelfAction(id))
                return BadRequest("You cannot suspend your own account");

            try
            {
                var (success, error) = await _setupService.SuspendUserAsync(id);
                if (!success)
                    return BadRequest(new { message = error });

                return Ok(new { message = "Account suspended successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suspending user {UserId}", id);
                return StatusCode(500, "An error occurred while suspending the user account");
            }
        }

        /// <summary>
        /// POST: api/adminapi/users/{id}/reactivate
        /// Reactivates a suspended user account
        /// </summary>
        [HttpPost("users/{id}/reactivate")]
        public async Task<IActionResult> ReactivateUser(int id)
        {
            try
            {
                var (success, error) = await _setupService.ReactivateUserAsync(id);
                if (!success)
                    return BadRequest(new { message = error });

                return Ok(new { message = "Account reactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reactivating user {UserId}", id);
                return StatusCode(500, "An error occurred while reactivating the user account");
            }
        }

        /// <summary>
        /// DELETE: api/adminapi/users/{id}
        /// Deletes a user account
        /// </summary>
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (IsSelfAction(id))
                return BadRequest("You cannot delete your own account");

            try
            {
                var (success, error) = await _setupService.DeleteUserAsync(id);
                if (!success)
                    return BadRequest(new { message = error });

                return Ok(new { message = "Account deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, "An error occurred while deleting the user account");
            }
        }

        private bool IsSelfAction(int targetUserId)
        {
            var claimVal = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("UserId")?.Value;
            return int.TryParse(claimVal, out var currentUserId) && currentUserId == targetUserId;
        }
    }
}