using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Gym.Models;
using Gym.Services;
using Microsoft.Extensions.Logging;
using Gym.Data;
using Microsoft.EntityFrameworkCore;

namespace Gym.Controllers
{
    [Route("trainer")]
    [Authorize(Roles = "Trainer,Admin,MainAdmin,FrontDesk,Frontdesk")]
    public class TrainerController : Controller
    {
        private readonly ITrainerService _trainerService;
        private readonly ILogger<TrainerController> _logger;
        private readonly GymDbContext _context;
        private readonly EquipmentEvidenceService _equipmentEvidence;

        public TrainerController(ITrainerService trainerService, ILogger<TrainerController> logger, GymDbContext context, EquipmentEvidenceService equipmentEvidence)
        {
            _trainerService = trainerService;
            _logger = logger;
            _context = context;
            _equipmentEvidence = equipmentEvidence;
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
                int trainerId = await GetCurrentTrainerProfileIdAsync();
                DateTime targetDate = date ?? DateTime.Today;

                TrainerDashboardViewModel viewModel = await _trainerService.GetTrainerAssignedScheduleAsync(trainerId, targetDate);

                // Fallback protection: Ensure collections are never null
                viewModel ??= new TrainerDashboardViewModel();
                viewModel.TodaysSessions ??= new List<SessionViewModel>();
                viewModel.WeeklySchedule ??= new Dictionary<string, int>();
                viewModel.AssignedWeek ??= new List<TrainerDayScheduleViewModel>();

                return View("TrainerDashboard", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading schedule for trainer {UserId}", GetCurrentUserId());

                // Temporary debug: pass error details or return empty view model instead of hard crash
                var fallbackModel = new TrainerDashboardViewModel
                {
                    SelectedDate = date?.Date ?? DateTime.Today,
                    TodaysSessions = new List<SessionViewModel>(),
                    WeeklySchedule = new Dictionary<string, int>(),
                    AssignedWeek = Enumerable.Range(0, 7)
                        .Select(offset => new TrainerDayScheduleViewModel
                        {
                            Date = (date?.Date ?? DateTime.Today)
                                .AddDays(-(((int)(date?.DayOfWeek ?? DateTime.Today.DayOfWeek) + 6) % 7))
                                .AddDays(offset)
                        }).ToList()
                };
                fallbackModel.WeekStart = fallbackModel.AssignedWeek[0].Date;

                return View("TrainerDashboard", fallbackModel);
            }
        }

        [HttpGet("members")]
        public async Task<IActionResult> Members(string? searchTerm, int page = 1)
        {
            try
            {
                const int pageSize = 8;
                page = Math.Max(1, page);
                searchTerm = searchTerm?.Trim();
                int trainerId = await GetCurrentTrainerProfileIdAsync();
                var query = _context.Clients.AsNoTracking()
                    .Where(client => client.AssignedTrainerId == trainerId);
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(client =>
                        client.FirstName.Contains(searchTerm) ||
                        client.LastName.Contains(searchTerm) ||
                        (client.FirstName + " " + client.LastName).Contains(searchTerm) ||
                        client.MembershipNumber.Contains(searchTerm));
                }

                var totalCount = await query.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                page = Math.Min(page, totalPages);
                var clients = await query.OrderBy(client => client.FirstName).ThenBy(client => client.LastName)
                    .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

                return View("Members", new TrainerMembersViewModel
                {
                    SearchTerm = searchTerm ?? string.Empty,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    Members = clients.Select(client => new TrainerMemberItemViewModel
                    {
                        ClientId = client.UserId,
                        FullName = $"{client.FirstName} {client.LastName}".Trim(),
                        Initials = $"{client.FirstName.FirstOrDefault()}{client.LastName.FirstOrDefault()}".ToUpperInvariant(),
                        Plan = client.MembershipPlan,
                        GymGoal = client.GymGoal?.ToString() switch
                        {
                            "MuscleGain" => "Muscle Gain",
                            "WeightLoss" => "Weight Loss",
                            "Cardio" => "Cardio",
                            _ => "Goal not set"
                        },
                        TrainingSchedule = string.IsNullOrWhiteSpace(client.PreferredDays)
                            ? "Not scheduled"
                            : $"{client.PreferredDays} · {client.PreferredTimeSlot ?? "Time not set"}",
                        JoinedDate = client.MembershipStartDate,
                        ExpirationDate = client.MembershipEndDate,
                        Status = client.Status
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading members list for trainer {UserId}", GetCurrentUserId());
                return View("Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
            }
        }

        [HttpGet("equipment")]
        public async Task<IActionResult> Equipment(string? searchTerm, int page = 1)
        {
            const int pageSize = 10;
            page = Math.Max(1, page);
            searchTerm = searchTerm?.Trim();

            var query = _context.Equipments.AsNoTracking().Where(item => item.Category != "Inventory" && item.Status != "Good" && item.Status != "Available" && item.Status != "In Use");
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(item =>
                    item.EquipmentName.Contains(searchTerm) ||
                    item.Category.Contains(searchTerm) ||
                    item.Status.Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            var items = await query.OrderBy(item => item.EquipmentName)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var allEquipment = await _context.Equipments.AsNoTracking().ToListAsync();

            return View(new FrontDeskEquipmentViewModel
            {
                SearchTerm = searchTerm ?? string.Empty,
                Items = items,
                EquipmentNames = await _context.EquipmentNameCatalog.AsNoTracking().OrderBy(item => item.Name).ToListAsync(),
                TotalAssets = allEquipment.Where(EquipmentStock.IsInventory).Sum(item => item.Quantity),
                InMaintenance = allEquipment.Where(item => item.Status == "Needs Maintenance").Sum(item => item.Quantity),
                OutOfOrder = allEquipment.Where(item => item.Status == "Out of Order").Sum(item => item.Quantity),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        [HttpPost("equipment/report")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEquipment(AddEquipmentViewModel form)
        {
            var allowedConditions = new[] { "Needs Maintenance", "Out of Order" };
            if (!allowedConditions.Contains(form.Condition))
            {
                ModelState.AddModelError(nameof(form.Condition), "Select a valid condition.");
            }

            await using var stockTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var normalizedName = form.EquipmentName?.Trim() ?? string.Empty;
            var catalogItem = await _context.EquipmentNameCatalog.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Name == normalizedName);
            if (catalogItem is null)
            {
                ModelState.AddModelError(nameof(form.EquipmentName),
                    "Select an equipment name created by an administrator.");
            }

            if (catalogItem is not null)
            {
                var stockRecords = await _context.Equipments.AsNoTracking().Where(item => item.EquipmentName == catalogItem.Name).ToListAsync();
                var stock = EquipmentStock.Summarize(new[] { catalogItem }, stockRecords).Single();
                if (form.Quantity > stock.Available)
                    ModelState.AddModelError(nameof(form.Quantity), $"Only {stock.Available} units are available to report.");
            }
            var hasIssue = form.Condition is "Fair" or "Needs Maintenance" or "Out of Order";
            if (hasIssue && (form.EvidencePhoto is null || form.EvidencePhoto.Length == 0))
            {
                ModelState.AddModelError(nameof(form.EvidencePhoto),
                    "Upload a photo showing the equipment issue.");
            }

            string? evidencePhotoPath = null;
            if (form.EvidencePhoto is { Length: > 0 })
            {
                var validationError = await _equipmentEvidence.ValidateAsync(form.EvidencePhoto);
                if (validationError is not null)
                {
                    ModelState.AddModelError(nameof(form.EvidencePhoto), validationError);
                }
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = string.Join(" ", ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message)));
                return RedirectToAction(nameof(Equipment));
            }

            if (form.EvidencePhoto is { Length: > 0 })
            {
                evidencePhotoPath = await _equipmentEvidence.SaveAsync(form.EvidencePhoto);
            }

            var equipment = new Equipment
            {
                EquipmentName = catalogItem!.Name,
                Category = "General",
                Quantity = form.Quantity,
                AvailableQuantity = 0,
                Status = form.Condition,
                AddedDate = DateTime.Now,
                PurchaseDate = catalogItem!.PurchaseDate,
                GymArrivalDate = catalogItem.GymArrivalDate,
                LastMaintenanceDate = null,
                EvidencePhotoPath = evidencePhotoPath
            };
            _context.Equipments.Add(equipment);
            try
            {
                await _context.SaveChangesAsync(); await stockTransaction.CommitAsync();
            }
            catch
            {
                _equipmentEvidence.Delete(evidencePhotoPath);
                throw;
            }
            TempData["SuccessMessage"] = "Equipment issue report submitted successfully.";
            return RedirectToAction(nameof(Equipment));
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> Notifications()
        {
            try
            {
                int trainerId = await GetCurrentTrainerProfileIdAsync();
                var notifications = await _trainerService.GetTrainerNotificationsAsync(trainerId);
                var unreadNotifications = await _context.Notifications
                    .Where(notification => notification.UserId == trainerId && !notification.IsRead)
                    .ToListAsync();
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                }
                if (unreadNotifications.Count > 0)
                {
                    await _context.SaveChangesAsync();
                }
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

        [HttpGet("availability")]
        public async Task<IActionResult> Availability()
        {
            var trainerId = await GetCurrentTrainerProfileIdAsync();
            var availability = await _context.TrainerAvailabilities
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TrainerId == trainerId);
            var assignedCount = await _context.Clients.AsNoTracking()
                .CountAsync(client => client.AssignedTrainerId == trainerId && client.IsActive && client.IsActiveMember);

            return View(new TrainerAvailabilityViewModel
            {
                SelectedDays = (availability?.AvailableDays ?? "Monday,Tuesday,Wednesday,Thursday,Friday")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                StartTime = availability?.StartTime ?? new TimeSpan(8, 0, 0),
                EndTime = availability?.EndTime ?? new TimeSpan(17, 0, 0),
                WeeklyMemberLimit = availability?.WeeklyMemberLimit ?? 10,
                CurrentlyAssignedMembers = assignedCount,
                LastUpdatedAt = availability?.UpdatedAt
            });
        }

        [HttpPost("availability")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Availability(TrainerAvailabilityViewModel model)
        {
            var allowedDays = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            model.SelectedDays = (model.SelectedDays ?? new List<string>())
                .Where(day => allowedDays.Contains(day)).Distinct().ToList();
            if (model.SelectedDays.Count == 0)
                ModelState.AddModelError(nameof(model.SelectedDays), "Select at least one available day.");
            if (model.EndTime <= model.StartTime)
                ModelState.AddModelError(nameof(model.EndTime), "End time must be later than start time.");

            var trainerId = await GetCurrentTrainerProfileIdAsync();
            model.CurrentlyAssignedMembers = await _context.Clients.AsNoTracking()
                .CountAsync(client => client.AssignedTrainerId == trainerId && client.IsActive && client.IsActiveMember);
            if (model.WeeklyMemberLimit < model.CurrentlyAssignedMembers)
                ModelState.AddModelError(nameof(model.WeeklyMemberLimit),
                    $"Capacity cannot be lower than your {model.CurrentlyAssignedMembers} currently assigned member(s).");
            if (!ModelState.IsValid) return View(model);

            var availability = await _context.TrainerAvailabilities
                .FirstOrDefaultAsync(item => item.TrainerId == trainerId);
            if (availability == null)
            {
                availability = new TrainerAvailability { TrainerId = trainerId };
                _context.TrainerAvailabilities.Add(availability);
            }
            availability.AvailableDays = string.Join(',', allowedDays.Where(day => model.SelectedDays.Contains(day)));
            availability.StartTime = model.StartTime;
            availability.EndTime = model.EndTime;
            availability.WeeklyMemberLimit = model.WeeklyMemberLimit;
            availability.UpdatedAt = DateTime.Now;
            var trainerName = await _context.Trainers.AsNoTracking()
                .Where(trainer => trainer.UserId == trainerId)
                .Select(trainer => (trainer.FirstName + " " + trainer.LastName).Trim())
                .FirstOrDefaultAsync() ?? "Trainer";
            _context.FrontDeskEmailNotifications.Add(new FrontDeskEmailNotification
            {
                StaffId = trainerId,
                Title = "Trainer availability changed",
                Message = $"{trainerName} updated availability to {availability.AvailableDays}, {availability.StartTime:hh\\:mm}–{availability.EndTime:hh\\:mm}, with a weekly capacity of {availability.WeeklyMemberLimit}.",
                Status = "Activity",
                EmailType = "Trainer availability",
                DeliveryKey = $"TrainerAvailability:{trainerId}:{DateTime.Now:yyyyMMddHHmmss}",
                CreatedDate = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your weekly availability was saved and will remain active until you change it.";
            return RedirectToAction(nameof(Availability));
        }

        private async Task<int> GetCurrentTrainerProfileIdAsync()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(email)) return GetCurrentUserId();

            var trainerId = await _context.Trainers.AsNoTracking()
                .Where(trainer => trainer.Email == email)
                .Select(trainer => trainer.UserId)
                .FirstOrDefaultAsync();
            if (trainerId > 0) return trainerId;
            if (!User.IsInRole("Trainer")) return GetCurrentUserId();

            var fullName = User.Identity?.Name ?? "Trainer";
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var trainer = new Trainer
            {
                FirstName = parts.Length > 1 ? string.Join(" ", parts[..^1]) : fullName,
                LastName = parts.Length > 1 ? parts[^1] : "Trainer",
                Email = email,
                PhoneNumber = User.FindFirst(ClaimTypes.MobilePhone)?.Value ?? string.Empty,
                Role = UserRole.Trainer,
                IsActive = true,
                CreatedDate = DateTime.Now
            };
            _context.Trainers.Add(trainer);
            await _context.SaveChangesAsync();
            return trainer.UserId;
        }

        #endregion
    }
}
