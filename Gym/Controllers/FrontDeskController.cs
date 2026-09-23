using Gym.Models;
using Gym.Services;
using Gym.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.Globalization;
using System.Security.Claims;

namespace Gym.Controllers
{
    public class FrontDeskController : Controller
    {
        private readonly IMemberService _memberService;
        private readonly GymDbContext _context;
        private readonly GmailValidationService _gmailService;
        private readonly SetupService _setupService;
        private readonly DailyMemberReminderSender _dailyReminderSender;
        private readonly ITimeLimitedDataProtector _reminderProtector;
        private readonly EquipmentEvidenceService _equipmentEvidence;

        public FrontDeskController(
            IMemberService memberService,
            GymDbContext context,
            GmailValidationService gmailService,
            SetupService setupService,
            DailyMemberReminderSender dailyReminderSender,
            IDataProtectionProvider dataProtectionProvider,
            EquipmentEvidenceService equipmentEvidence)
        {
            _memberService = memberService;
            _context = context;
            _gmailService = gmailService;
            _setupService = setupService;
            _dailyReminderSender = dailyReminderSender;
            _equipmentEvidence = equipmentEvidence;
            _reminderProtector = dataProtectionProvider
                .CreateProtector("Gym.TrainerSessionReminder.v1")
                .ToTimeLimitedDataProtector();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendDailyMemberReminder(int id)
        {
            var (success, message) = await _dailyReminderSender.SendAsync(id, DateTime.Today);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
            return RedirectToAction(nameof(Payment));
        }

        [HttpGet]
        public async Task<IActionResult> Fdashboard()
        {
            var weekStart = DateTime.Today.AddDays(-6);
            var weekEnd = DateTime.Today.AddDays(1);

            var memberCheckIns = await _context.MemberAttendanceLogs
                .AsNoTracking()
                .Where(attendance => attendance.CheckInAt >= weekStart &&
                    attendance.CheckInAt < weekEnd)
                .Select(attendance => attendance.CheckInAt)
                .ToListAsync();

            var walkInRegistrations = await _context.WalkInClients
                .AsNoTracking()
                .Where(walkIn => walkIn.RegisteredAt >= weekStart &&
                    walkIn.RegisteredAt < weekEnd)
                .Select(walkIn => walkIn.RegisteredAt)
                .ToListAsync();

            var expiringSoonMemberships = await _context.Clients
                .AsNoTracking()
                .CountAsync(client => client.IsActive && client.IsActiveMember &&
                    client.MembershipEndDate.HasValue &&
                    client.MembershipEndDate.Value >= DateTime.Today &&
                    client.MembershipEndDate.Value < DateTime.Today.AddDays(8));

            var equipmentNeedingMaintenance = await _context.Equipments
                .AsNoTracking()
                .Where(equipment => equipment.Status == "Needs Maintenance")
                .SumAsync(equipment => equipment.Quantity);

            var registeredTrainers = await _setupService.GetActiveRegisteredTrainersAsync();
            var registeredTrainerEmails = registeredTrainers
                .Select(trainer => trainer.Email)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .ToList();
            var activeTrainers = await _context.Trainers.AsNoTracking()
                .Where(trainer => trainer.IsActive && registeredTrainerEmails.Contains(trainer.Email))
                .ToListAsync();
            var trainerProfilesByEmail = activeTrainers
                .ToDictionary(trainer => trainer.Email, StringComparer.OrdinalIgnoreCase);
            var activeTrainerIds = activeTrainers.Select(trainer => trainer.UserId).ToList();
            var trainerAvailability = await _context.TrainerAvailabilities.AsNoTracking()
                .Where(item => activeTrainerIds.Contains(item.TrainerId))
                .ToDictionaryAsync(item => item.TrainerId);
            var trainerAssignedCounts = await _context.Clients.AsNoTracking()
                .Where(client => client.AssignedTrainerId.HasValue &&
                    activeTrainerIds.Contains(client.AssignedTrainerId.Value) &&
                    client.IsActive && client.IsActiveMember)
                .GroupBy(client => client.AssignedTrainerId!.Value)
                .Select(group => new { TrainerId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.TrainerId, item => item.Count);

            var today = DateTime.Today;
            var fullDay = today.ToString("dddd", CultureInfo.InvariantCulture);
            var shortDay = today.ToString("ddd", CultureInfo.InvariantCulture);
            var assignedClients = (await _context.Clients.AsNoTracking()
                    .Where(client => client.AssignedTrainerId.HasValue && client.IsActive && client.IsActiveMember)
                    .ToListAsync())
                .Where(client => (client.PreferredDays ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(day => day.Equals(fullDay, StringComparison.OrdinalIgnoreCase) ||
                                day.Equals(shortDay, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var trainerIds = assignedClients.Select(client => client.AssignedTrainerId!.Value).Distinct().ToList();
            var trainers = await _context.Trainers.AsNoTracking()
                .Where(trainer => trainerIds.Contains(trainer.UserId))
                .ToDictionaryAsync(trainer => trainer.UserId);
            var responsePrefix = $"TrainingResponse:%:{today:yyyyMMdd}:%";
            var responses = await _context.Notifications.AsNoTracking()
                .Where(notification => EF.Functions.Like(notification.NotificationType, responsePrefix))
                .OrderByDescending(notification => notification.CreatedDate)
                .ToListAsync();
            var reminderPrefix = $"TrainingReminderSent:%:{today:yyyyMMdd}";
            var sentReminders = await _context.Notifications.AsNoTracking()
                .Where(notification => EF.Functions.Like(notification.NotificationType, reminderPrefix))
                .Select(notification => notification.NotificationType)
                .ToListAsync();

            var unreadNotificationsCount = 0;
            if (TryGetCurrentUserId(out var currentUserId))
            {
                unreadNotificationsCount = await _context.FrontDeskEmailNotifications
                    .AsNoTracking()
                    .CountAsync(item => (item.StaffId == 0 || item.StaffId == currentUserId) && item.Status == "New");
            }

            var model = new FrontDeskDashboardViewModel
            {
                ExpiringSoonMemberships = expiringSoonMemberships,
                WeeklyWalkIns = walkInRegistrations.Count,
                EquipmentNeedingMaintenance = equipmentNeedingMaintenance,
                UnreadNotificationsCount = unreadNotificationsCount,
                TrainerAvailability = registeredTrainers.Select(registeredTrainer =>
                {
                    trainerProfilesByEmail.TryGetValue(registeredTrainer.Email, out var trainerProfile);
                    var trainerId = trainerProfile?.UserId ?? 0;
                    trainerAvailability.TryGetValue(trainerId, out var availability);
                    trainerAssignedCounts.TryGetValue(trainerId, out var assigned);
                    return new FrontDeskTrainerAvailabilityItem
                    {
                        TrainerName = registeredTrainer.FullName,
                        AvailableDays = availability?.AvailableDays ?? "Not set",
                        StartTime = availability?.StartTime,
                        EndTime = availability?.EndTime,
                        AssignedMembers = assigned,
                        WeeklyMemberLimit = availability?.WeeklyMemberLimit
                    };
                }).ToList(),
                WeeklyTraffic = Enumerable.Range(0, 7)
                    .Select(offset =>
                    {
                        var date = weekStart.AddDays(offset);

                        return new FrontDeskWeeklyTrafficPoint
                        {
                            Day = date.ToString("ddd"),
                            MemberAttendance = memberCheckIns.Count(checkIn =>
                                checkIn.Date == date),
                            WalkIns = walkInRegistrations.Count(registration =>
                                registration.Date == date)
                        };
                    }).ToList(),
                Schedule = assignedClients.Select(client =>
                {
                    var trainerId = client.AssignedTrainerId!.Value;
                    trainers.TryGetValue(trainerId, out var trainer);
                    ParseTrainingTime(today, client.PreferredTimeSlot, out var startTime, out var endTime);
                    var timeSignature = GetTimeSignature(startTime, endTime);
                    var response = responses.FirstOrDefault(item =>
                        item.NotificationType.StartsWith($"TrainingResponse:{client.UserId}:{today:yyyyMMdd}:{timeSignature}:", StringComparison.Ordinal));
                    return new FrontDeskScheduleItem
                    {
                        ClientId = client.UserId,
                        TrainerId = trainerId,
                        TrainerName = trainer == null ? "Trainer unavailable" : $"{trainer.FirstName} {trainer.LastName}".Trim(),
                        MemberName = $"{client.FirstName} {client.LastName}".Trim(),
                        Date = today,
                        StartTime = startTime,
                        EndTime = endTime,
                        Status = SessionStatus.Scheduled,
                        DisplayStatus = response?.NotificationType.Split(':').LastOrDefault() ?? "Scheduled",
                        IsReminded = sentReminders.Contains($"TrainingReminderSent:{client.UserId}:{today:yyyyMMdd}:{timeSignature}")
                    };
                }).OrderBy(item => item.StartTime).ThenBy(item => item.MemberName).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Notifications(int page = 1)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            var unreadNotifications = await _context.FrontDeskEmailNotifications
                .Where(item => (item.StaffId == 0 || item.StaffId == userId) && item.Status == "New")
                .ToListAsync();
            foreach (var notification in unreadNotifications)
            {
                notification.Status = "Read";
            }
            if (unreadNotifications.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            const int pageSize = 5;
            var notificationQuery = _context.FrontDeskEmailNotifications
                .AsNoTracking()
                .Where(item => item.StaffId == 0 || item.StaffId == userId)
                .OrderByDescending(item => item.CreatedDate);
            var totalCount = await notificationQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var notifications = await notificationQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendSessionReminder(int clientId, DateTime date)
        {
            var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == clientId);
            if (client?.AssignedTrainerId == null || !client.IsActive || !client.IsActiveMember)
            {
                TempData["ErrorMessage"] = "The assigned member could not be found.";
                return RedirectToAction(nameof(Fdashboard));
            }

            var dayNames = new[] { date.ToString("dddd", CultureInfo.InvariantCulture), date.ToString("ddd", CultureInfo.InvariantCulture) };
            var isAssignedThatDay = (client.PreferredDays ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(day => dayNames.Contains(day, StringComparer.OrdinalIgnoreCase));
            if (!isAssignedThatDay)
            {
                TempData["ErrorMessage"] = "This member is not assigned for the selected day.";
                return RedirectToAction(nameof(Fdashboard));
            }

            var trainer = await _context.Trainers.AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == client.AssignedTrainerId.Value);
            ParseTrainingTime(date.Date, client.PreferredTimeSlot, out var startTime, out var endTime);
            if (startTime <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "A reminder cannot be sent because this training session has already started or passed.";
                return RedirectToAction(nameof(Fdashboard));
            }
            var timeSignature = GetTimeSignature(startTime, endTime);
            var payload = $"{client.UserId}|{client.AssignedTrainerId.Value}|{date:yyyyMMdd}|{timeSignature}";
            var token = _reminderProtector.Protect(payload, TimeSpan.FromHours(48));
            var confirmUrl = Url.Action(nameof(SessionReminderResponse), "FrontDesk", new { token, response = "Confirmed" }, Request.Scheme)!;
            var cancelUrl = Url.Action(nameof(SessionReminderResponse), "FrontDesk", new { token, response = "Cancelled" }, Request.Scheme)!;
            var (sent, error) = await _gmailService.SendTrainerSessionReminderAsync(
                client.Email, $"{client.FirstName} {client.LastName}".Trim(),
                trainer == null ? "Assigned trainer" : $"{trainer.FirstName} {trainer.LastName}".Trim(),
                startTime, endTime, confirmUrl, cancelUrl);
            if (sent)
            {
                var reminderType = $"TrainingReminderSent:{client.UserId}:{date:yyyyMMdd}:{timeSignature}";
                if (!await _context.Notifications.AnyAsync(item => item.NotificationType == reminderType))
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = client.AssignedTrainerId.Value,
                        Title = "Session reminder sent",
                        Message = $"A training reminder was sent to {client.FirstName} {client.LastName} for {date:MMMM d, yyyy}.",
                        NotificationType = reminderType,
                        IsRead = true,
                        CreatedDate = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
            }
            await RecordEmailDeliveryAsync(
                sent,
                "Session reminder",
                $"{client.FirstName} {client.LastName}".Trim(),
                client.Email,
                error);
            TempData[sent ? "SuccessMessage" : "ErrorMessage"] = sent
                ? $"Session reminder sent to {client.Email}."
                : error ?? "The reminder could not be sent.";
            return RedirectToAction(nameof(Fdashboard));
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SessionReminderResponse(string token, string response)
        {
            if (response is not ("Confirmed" or "Cancelled")) return BadRequest("Invalid response.");
            try
            {
                var payload = _reminderProtector.Unprotect(token, out _);
                var values = payload.Split('|');
                if (values.Length != 4 || !int.TryParse(values[0], out var clientId) ||
                    !int.TryParse(values[1], out var trainerId) ||
                    !DateTime.TryParseExact(values[2], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sessionDate))
                    return BadRequest("Invalid reminder link.");
                var timeSignature = values[3];
                if (timeSignature.Length != 9 || timeSignature[4] != '-' ||
                    !timeSignature.Remove(4, 1).All(char.IsDigit))
                    return BadRequest("Invalid reminder link.");

                var client = await _context.Clients.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.UserId == clientId && item.AssignedTrainerId == trainerId);
                if (client == null) return NotFound("The assigned member was not found.");
                var type = $"TrainingResponse:{clientId}:{sessionDate:yyyyMMdd}:{timeSignature}:{response}";
                var alreadyRecorded = await _context.Notifications.AnyAsync(item => item.NotificationType == type);
                if (!alreadyRecorded)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = trainerId,
                        Title = response == "Confirmed" ? "Training session confirmed" : "Training session cancelled",
                        Message = $"{client.FirstName} {client.LastName} has {response.ToLowerInvariant()} the training session on {sessionDate:MMMM d, yyyy}.",
                        NotificationType = type,
                        CreatedDate = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }

                var trainerName = await _context.Trainers.AsNoTracking()
                    .Where(item => item.UserId == trainerId)
                    .Select(item => (item.FirstName + " " + item.LastName).Trim())
                    .FirstOrDefaultAsync() ?? "your trainer";
                var responseModel = new SessionReminderResponseViewModel
                {
                    Response = response,
                    MemberName = $"{client.FirstName} {client.LastName}".Trim(),
                    TrainerName = trainerName,
                    SessionDate = sessionDate,
                    Message = response == "Confirmed"
                        ? "Your attendance is confirmed. Your trainer has been notified that you will attend the gym session."
                        : "Your session has been cancelled. Your trainer has been notified that you cannot attend the gym session."
                };
                return View(response == "Confirmed" ? "SessionConfirmed" : "SessionCancelled", responseModel);
            }
            catch
            {
                return BadRequest("This reminder link is invalid or has expired.");
            }
        }

        private static void ParseTrainingTime(DateTime date, string? timeSlot, out DateTime startTime, out DateTime endTime)
        {
            startTime = date.Date.AddHours(9);
            endTime = startTime.AddHours(1);
            var parts = (timeSlot ?? string.Empty).Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return;
            if (DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out var parsedStart))
                startTime = date.Date.Add(parsedStart.TimeOfDay);
            if (DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out var parsedEnd))
                endTime = date.Date.Add(parsedEnd.TimeOfDay);
            if (endTime <= startTime) endTime = startTime.AddHours(1);
        }

        private static string GetTimeSignature(DateTime startTime, DateTime endTime) =>
            $"{startTime:HHmm}-{endTime:HHmm}";

        [HttpGet]
        public async Task<IActionResult> WalkInClient(string? searchTerm, int page = 1)
        {
            ViewBag.WalkInPaymentTypes = await _context.WalkInPaymentTypes.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync();
            var query = _context.WalkInClients.AsNoTracking();
            searchTerm = searchTerm?.Trim();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(item =>
                    item.FirstName.Contains(searchTerm) ||
                    item.LastName.Contains(searchTerm) ||
                    (item.FirstName + " " + item.LastName).Contains(searchTerm) ||
                    item.ContactNumber.Contains(searchTerm) ||
                    (item.Email != null && item.Email.Contains(searchTerm)) ||
                    item.ReasonForVisit.Contains(searchTerm));
            }
            var matchingRegistrations = await query
                .OrderByDescending(item => item.RegisteredAt)
                .ToListAsync();
            var allRegistrations = matchingRegistrations
                .GroupBy(item => !string.IsNullOrWhiteSpace(item.Email)
                    ? $"email:{item.Email.Trim().ToLowerInvariant()}"
                    : !string.IsNullOrWhiteSpace(item.ContactNumber)
                        ? $"contact:{new string(item.ContactNumber.Where(char.IsDigit).ToArray())}"
                        : $"name:{item.FirstName.Trim().ToLowerInvariant()}:{item.LastName.Trim().ToLowerInvariant()}")
                .Select(group =>
                {
                    var latest = group.OrderByDescending(item => item.RegisteredAt).First();
                    return new WalkInClientListItem
                    {
                        WalkInClientId = latest.WalkInClientId,
                        FirstName = latest.FirstName,
                        LastName = latest.LastName,
                        ContactNumber = latest.ContactNumber,
                        Email = latest.Email,
                        ReasonForVisit = latest.ReasonForVisit,
                        FirstRegisteredAt = group.Min(item => item.RegisteredAt),
                        LastVisitAt = latest.RegisteredAt,
                        VisitCount = group.Count()
                    };
                })
                .OrderByDescending(item => item.LastVisitAt)
                .ToList();
            const int pageSize = 5;
            var totalCount = allRegistrations.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var registrations = allRegistrations
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.SearchTerm = searchTerm ?? string.Empty;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            return View(registrations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordWalkInPayment(int id, decimal amount, int? paymentTypeId)
        {
            var rate = await _context.WalkInPaymentTypes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == paymentTypeId && item.IsActive);
            if (rate == null)
            {
                TempData["ErrorMessage"] = "Select an active walk-in payment type.";
                return RedirectToAction(nameof(WalkInClient));
            }
            amount = rate.Amount;            if (amount <= 0)
            {
                TempData["ErrorMessage"] = "The cash payment amount must be greater than zero.";
                return RedirectToAction(nameof(WalkInClient));
            }

            var client = await _context.WalkInClients
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.WalkInClientId == id);
            if (client == null)
            {
                TempData["ErrorMessage"] = "The walk-in client could not be found.";
                return RedirectToAction(nameof(WalkInClient));
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var alreadyRegisteredToday = await _context.WalkInClients.AsNoTracking()
                .AnyAsync(item => item.RegisteredAt >= today && item.RegisteredAt < tomorrow &&
                    ((!string.IsNullOrWhiteSpace(client.Email) && item.Email == client.Email) ||
                     item.ContactNumber == client.ContactNumber));
            if (alreadyRegisteredToday)
            {
                TempData["ErrorMessage"] = "This walk-in client has already been registered today.";
                return RedirectToAction(nameof(WalkInClient));
            }

            var registeredAt = DateTime.Now;
            _context.WalkInClients.Add(new WalkInClient
            {
                FirstName = client.FirstName,
                LastName = client.LastName,
                ContactNumber = client.ContactNumber,
                Email = client.Email,
                PaymentTypeName = rate.Name, ReasonForVisit = client.ReasonForVisit,
                PaymentMethod = "Cash",
                AmountPaid = amount,
                IssueDigitalReceipt = client.IssueDigitalReceipt,
                LiabilityAccepted = client.LiabilityAccepted,
                Status = "Checked-in",
                RegisteredAt = registeredAt
            });
            await _context.SaveChangesAsync();

            var fullName = $"{client.FirstName} {client.LastName}".Trim();
            if (client.IssueDigitalReceipt && !string.IsNullOrWhiteSpace(client.Email))
            {
                var (sent, error) = await _gmailService.SendWalkInReceiptAsync(
                    client.Email, fullName, amount, registeredAt, "Cash");
                await RecordEmailDeliveryAsync(
                    sent, "Walk-in receipt", fullName, client.Email, error);
                TempData[sent ? "SuccessMessage" : "ErrorMessage"] = sent
                    ? $"A new cash visit for {fullName} was recorded and the receipt was emailed."
                    : error;
            }
            else
            {
                await RecordEmailDeliveryAsync(
                    false,
                    "Walk-in receipt",
                    fullName,
                    "No email address",
                    "The walk-in client did not provide an email address.");
                TempData["SuccessMessage"] = $"A new cash visit for {fullName} was recorded successfully.";
            }

            return RedirectToAction(nameof(WalkInClient));
        }

        [HttpGet]
        public async Task<IActionResult> Payment(
            string? searchTerm,
            PaymentStatus? selectedStatus,
            int page = 1)
        {
            const int pageSize = 10;
            page = Math.Max(1, page);
            searchTerm = searchTerm?.Trim();

            var query = _context.Payments
                .AsNoTracking()
                .Include(payment => payment.Client)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(payment =>
                    payment.TransactionCode.Contains(searchTerm) ||
                    payment.Client.FirstName.Contains(searchTerm) ||
                    payment.Client.LastName.Contains(searchTerm) ||
                    (payment.Client.FirstName + " " + payment.Client.LastName)
                        .Contains(searchTerm));
            }

            if (selectedStatus.HasValue)
            {
                query = query.Where(payment => payment.Status == selectedStatus.Value);
            }

            var matchingPayments = await query
                .OrderByDescending(payment => payment.PaymentDate)
                .ThenByDescending(payment => payment.PaymentId)
                .ToListAsync();

            var uniquePayments = matchingPayments
                .GroupBy(payment => new
                {
                    payment.ClientId,
                    payment.Amount,
                    Date = payment.PaymentDate.Date,
                    payment.Status
                })
                .Select(group => group.First())
                .ToList();

            var totalCount = uniquePayments.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);

            var paymentClientIds = uniquePayments.Select(payment => payment.ClientId).Distinct().ToList();
            var paidTotals = await _context.Payments.AsNoTracking()
                .Where(payment => paymentClientIds.Contains(payment.ClientId) && payment.Status == PaymentStatus.Paid)
                .GroupBy(payment => payment.ClientId)
                .ToDictionaryAsync(group => group.Key, group => group.Sum(payment => payment.Amount));

            var payments = uniquePayments
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(payment => new FrontDeskPaymentItem
                {
                    PaymentId = payment.PaymentId,
                    ClientId = payment.ClientId,
                    TransactionCode = payment.TransactionCode,
                    MemberName = (payment.Client.FirstName + " " + payment.Client.LastName).Trim(),
                    MemberEmail = payment.Client.Email,
                    MembershipNumber = payment.Client.MembershipNumber,
                    Plan = payment.Client.MembershipPlan,
                    PlanPrice = MembershipPlanPricing.GetPrice(payment.Client.MembershipPlan),
                    TotalPaid = paidTotals.GetValueOrDefault(payment.ClientId),
                    RemainingBalance = Math.Max(0m, MembershipPlanPricing.GetPrice(payment.Client.MembershipPlan) - paidTotals.GetValueOrDefault(payment.ClientId)),
                    MemberStatus = payment.Client.Status,
                    MembershipEndDate = payment.Client.MembershipEndDate,
                    Initials = (payment.Client.FirstName.Substring(0, 1) +
                        payment.Client.LastName.Substring(0, 1)).ToUpper(),
                    PaymentDate = payment.PaymentDate,
                    Amount = payment.Amount,
                    Status = payment.Status
                })
                .ToList();

            return View(new FrontDeskPaymentViewModel
            {
                SearchTerm = searchTerm ?? string.Empty,
                SelectedStatus = selectedStatus,
                Payments = payments,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        [HttpGet]
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
            var items = await query
                .OrderBy(item => item.EquipmentName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var allEquipment = await _context.Equipments.AsNoTracking().ToListAsync();
            var equipmentNames = await _context.EquipmentNameCatalog.AsNoTracking()
                .OrderBy(item => item.Name)
                .ToListAsync();
            return View(new FrontDeskEquipmentViewModel
            {
                SearchTerm = searchTerm ?? string.Empty,
                Items = items,
                EquipmentNames = equipmentNames,
                TotalAssets = allEquipment.Where(EquipmentStock.IsInventory).Sum(item => item.Quantity),
                InMaintenance = allEquipment
                    .Where(item => item.Status == "Needs Maintenance")
                    .Sum(item => item.Quantity),
                OutOfOrder = allEquipment
                    .Where(item => item.Status == "Out of Order")
                    .Sum(item => item.Quantity),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        [HttpPost]
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

        [HttpGet]
        public async Task<IActionResult> AttendanceMonitoring(
            string? searchTerm,
            int page = 1)
        {
            const int pageSize = 10;
            page = Math.Max(1, page);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var clients = await _context.Clients.AsNoTracking()
                .Where(client => client.Role == UserRole.Client &&
                    client.IsActive && client.IsActiveMember &&
                    (!client.MembershipEndDate.HasValue || client.MembershipEndDate.Value >= today))
                .OrderBy(client => client.LastName)
                .ThenBy(client => client.FirstName)
                .ToListAsync();
            var logs = await _context.MemberAttendanceLogs.AsNoTracking()
                .Where(log => log.CheckInAt >= today && log.CheckInAt < tomorrow)
                .OrderByDescending(log => log.CheckInAt)
                .ToListAsync();

            var items = clients.Select(client =>
            {
                var log = logs.FirstOrDefault(item => item.ClientId == client.UserId);
                var name = $"{client.FirstName} {client.LastName}".Trim();
                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var initials = parts.Length > 1
                    ? $"{parts[0][0]}{parts[^1][0]}" : name[..Math.Min(2, name.Length)];
                return new AttendanceMemberItem
                {
                    ClientId = client.UserId,
                    FullName = name,
                    Initials = initials.ToUpperInvariant(),
                    Membership = MembershipPlanPricing.GetName(client.MembershipPlan),
                    CheckInAt = log?.CheckInAt,
                    Status = log == null ? "Not checked in" : "Checked in"
                };
            }).ToList();

            searchTerm = searchTerm?.Trim();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                items = items.Where(item =>
                    item.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    item.ClientId.ToString().Contains(searchTerm) ||
                    item.Status.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    item.Membership.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var totalCount = items.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            return View(new AttendanceMonitoringViewModel
            {
                SearchTerm = searchTerm ?? string.Empty,
                Members = items.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                Page = page, PageSize = pageSize, TotalCount = totalCount
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAttendance(int id, string attendanceAction)
        {
            var clientExists = await _context.Clients.AnyAsync(client =>
                client.UserId == id && client.Role == UserRole.Client);
            if (!clientExists)
            {
                TempData["ErrorMessage"] = "Member was not found.";
                return RedirectToAction(nameof(AttendanceMonitoring));
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            if (!string.Equals(attendanceAction, "checkin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Only member check-in is available.";
                return RedirectToAction(nameof(AttendanceMonitoring));
            }

            var alreadyCheckedIn = await _context.MemberAttendanceLogs
                .AsNoTracking()
                .AnyAsync(log => log.ClientId == id &&
                    log.CheckInAt >= today && log.CheckInAt < tomorrow);
            if (alreadyCheckedIn)
            {
                TempData["ErrorMessage"] = "This member has already checked in today.";
                return RedirectToAction(nameof(AttendanceMonitoring));
            }

            _context.MemberAttendanceLogs.Add(new MemberAttendanceLog
            {
                ClientId = id,
                CheckInAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Member checked in successfully.";
            return RedirectToAction(nameof(AttendanceMonitoring));
        }

        [HttpGet]
        public async Task<IActionResult> WalkInRegister()
        {
            ViewBag.WalkInPaymentTypes = await _context.WalkInPaymentTypes.AsNoTracking()
                .Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync();
            return View(new WalkInRegistrationViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WalkInRegister(WalkInRegistrationViewModel form)
        {
            if (!await _context.WalkInPaymentTypes.AnyAsync(item => item.IsActive))
            {
                TempData["ErrorMessage"] = "Walk-in registration is unavailable. An administrator must add an active walk-in payment type first.";
                return RedirectToAction(nameof(WalkInClient));
            }
            var rate = await _context.WalkInPaymentTypes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == form.PaymentTypeId && item.IsActive);
            if (rate == null)
            {
                TempData["ErrorMessage"] = "Select an active walk-in payment type.";
                return RedirectToAction(nameof(WalkInClient));
            }
            form.AmountPaid = rate.Amount;
            ModelState.Remove(nameof(form.AmountPaid));
            ModelState.Remove(nameof(form.IssueDigitalReceipt));
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please complete all required walk-in registration fields.";
                return RedirectToAction(nameof(WalkInClient));
            }

            var firstName = form.FirstName.Trim();
            var lastName = form.LastName.Trim();
            var contact = form.ContactNumber.Trim();
            var email = string.IsNullOrWhiteSpace(form.Email) ? null : form.Email.Trim();
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var alreadyRegisteredToday = await _context.WalkInClients.AsNoTracking()
                .AnyAsync(item => item.RegisteredAt >= today && item.RegisteredAt < tomorrow &&
                    ((!string.IsNullOrWhiteSpace(email) && item.Email == email) ||
                     item.ContactNumber == contact ||
                     (item.FirstName == firstName && item.LastName == lastName)));
            if (alreadyRegisteredToday)
            {
                TempData["ErrorMessage"] = "This walk-in client has already been registered today. Registration is available again tomorrow.";
                return RedirectToAction(nameof(WalkInClient));
            }

            var registeredAt = DateTime.Now;
            _context.WalkInClients.Add(new WalkInClient
            {
                FirstName = firstName, LastName = lastName,
                ContactNumber = contact,
                Email = email,
                PaymentTypeName = rate.Name, ReasonForVisit = form.ReasonForVisit, PaymentMethod = "Cash",
                AmountPaid = form.AmountPaid, IssueDigitalReceipt = email != null,
                LiabilityAccepted = false, Status = "Checked-in",
                RegisteredAt = registeredAt
            });
            await _context.SaveChangesAsync();

            if (email != null)
            {
                var (sent, error) = await _gmailService.SendWalkInReceiptAsync(
                    email, $"{firstName} {lastName}",
                    form.AmountPaid, registeredAt, "Cash");
                await RecordEmailDeliveryAsync(
                    sent,
                    "Walk-in receipt",
                    $"{form.FirstName} {form.LastName}".Trim(),
                    email,
                    error);
                TempData[sent ? "SuccessMessage" : "ErrorMessage"] = sent
                    ? "Walk-in client registered and the digital receipt was emailed successfully."
                    : error;
            }
            else
            {
                await RecordEmailDeliveryAsync(
                    false,
                    "Walk-in receipt",
                    $"{firstName} {lastName}".Trim(),
                    "No email address",
                    "The walk-in client did not provide an email address.");
                TempData["SuccessMessage"] = "Walk-in client registered successfully.";
            }
            return RedirectToAction(nameof(WalkInClient));
        }

        [HttpGet]
        public async Task<IActionResult> Member(
            string? searchTerm,
            MemberStatus? selectedStatus,
            MembershipPlanType? selectedPlan,
            int page = 1,
            int pageSize = 10,
            bool openDrawer = false)
        {
            var viewModel = await BuildMembersPageViewModelAsync(
                searchTerm,
                selectedStatus,
                selectedPlan,
                page,
                pageSize,
                openDrawer);

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> AddMember(bool returnToAdmin = false)
        {
            var viewModel = await BuildMembersPageViewModelAsync(
                searchTerm: null,
                selectedStatus: null,
                selectedPlan: null,
                page: 1,
                pageSize: 10,
                openDrawer: false);

            ViewBag.ReturnToAdmin = returnToAdmin;
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(
            [Bind(Prefix = "AddMemberViewModel")]
            AddMemberViewModel form,
            bool returnToAdmin = false)
        {
            ViewBag.ReturnToAdmin = returnToAdmin;
            if (!ModelState.IsValid)
            {
                form = await _memberService
                    .BuildAddMemberViewModelAsync(form);

                var listModel = await _memberService
                    .GetMemberListAsync(null, null, null, 1, 10);

                return View("AddMember", new MembersPageViewModel
                {
                    ListViewModel = listModel,
                    AddMemberViewModel = form
                });
            }

            Client registeredMember;
            try
            {
                registeredMember = await _memberService.CreateClientAsync(form);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);

                form = await _memberService
                    .BuildAddMemberViewModelAsync(form);

                var listModel = await _memberService
                    .GetMemberListAsync(null, null, null, 1, 10);

                return View("AddMember", new MembersPageViewModel
                {
                    ListViewModel = listModel,
                    AddMemberViewModel = form
                });
            }

            var initialPayment = registeredMember.Payments.Single();
            var (receiptSent, receiptError) = await _gmailService.SendMemberPaymentReceiptAsync(
                registeredMember.Email, form.FullName, initialPayment.TransactionCode,
                initialPayment.Amount, initialPayment.PaymentDate,
                MembershipPlanPricing.GetName(registeredMember.MembershipPlan),
                initialPayment.Amount, initialPayment.Amount, 0m);
            await RecordEmailDeliveryAsync(receiptSent, "Payment receipt", form.FullName,
                registeredMember.Email, receiptError);
            TempData[receiptSent ? "SuccessMessage" : "ErrorMessage"] = receiptSent
                ? $"Member '{form.FullName}' registered, payment recorded, and receipt emailed successfully."
                : $"Member '{form.FullName}' registered and payment recorded, but the receipt could not be emailed. {receiptError}";

            return returnToAdmin
                ? RedirectToAction("Members", "Admin", new { registrationComplete = true })
                : RedirectToAction(nameof(Member), new { registrationComplete = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMember(int id)
        {
            await _memberService.DeleteClientAsync(id);

            TempData["SuccessMessage"] =
                "Member marked as inactive.";

            return RedirectToAction(nameof(Member));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkInactive(int id)
        {
            var member = await _context.Clients.AsNoTracking()
                .FirstOrDefaultAsync(client => client.UserId == id && client.Role == UserRole.Client);
            if (member?.MembershipEndDate == null || member.MembershipEndDate.Value.Date >= DateTime.Today)
            {
                TempData["ErrorMessage"] = "An active membership cannot be marked inactive before its plan ends.";
                return RedirectToAction(nameof(Member));
            }

            await _memberService.DeleteClientAsync(id);
            TempData["SuccessMessage"] = "The ended membership was marked inactive.";

            return RedirectToAction(nameof(Member));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateMember(int id)
        {
            await _memberService.ReactivateClientAsync(id);

            TempData["SuccessMessage"] =
                "Member reactivated successfully.";

            return RedirectToAction(nameof(Member));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenewMembership(int id, decimal amount)
        {
            var member = await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(client => client.UserId == id && client.Role == UserRole.Client);

            if (member == null || member.Status == MemberStatus.Terminated)
            {
                TempData["ErrorMessage"] = "The member could not be renewed.";
                return RedirectToAction(nameof(Payment));
            }

            if (!MembershipRenewalRules.CanRenew(member.Status, member.MembershipEndDate, DateTime.Today))
            {
                TempData["ErrorMessage"] = "Renewal opens 7 days before the membership ends. Cancelled memberships can restart at the full plan price.";
                return RedirectToAction(nameof(Payment));
            }

            var planPrice = MembershipPlanPricing.GetPrice(member.MembershipPlan);
            if (amount != planPrice)
            {
                TempData["ErrorMessage"] = $"The full renewal payment of ₱{planPrice:N2} is required.";
                return RedirectToAction(nameof(Payment));
            }

            var paidAt = DateTime.Now;
            var transactionCode = $"TXN-{paidAt:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            _context.Payments.Add(new Payment
            {
                ClientId = id,
                Amount = amount,
                Status = PaymentStatus.Paid,
                TransactionCode = transactionCode,
                PaymentDate = paidAt,
                DueDate = paidAt,
                Notes = "Cash membership renewal payment recorded by Front Desk."
            });

            var newExpirationDate = await _memberService
                .RenewMembershipAsync(id);

            if (!newExpirationDate.HasValue)
            {
                TempData["ErrorMessage"] =
                    "The membership could not be renewed. Terminated or missing members cannot be renewed.";
            }
            else
            {
                await RecordAdminActivityAsync(
                    "Membership renewed",
                    $"{member.FirstName} {member.LastName}".Trim(),
                    $"Membership renewed through {newExpirationDate.Value:MMM d, yyyy}.");

                var memberName = $"{member.FirstName} {member.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(member.Email))
                {
                    await RecordEmailDeliveryAsync(false, "Payment receipt", memberName, "No email address", "The member has no email address.");
                    TempData["ErrorMessage"] =
                        $"Payment received and membership renewed through {newExpirationDate.Value:MMM d, yyyy}, but the member has no email address for the digital receipt.";
                }
                else
                {
                    var (sent, error) = await _gmailService.SendMemberPaymentReceiptAsync(
                        member.Email, memberName, transactionCode, amount, paidAt,
                        MembershipPlanPricing.GetName(member.MembershipPlan), planPrice, amount, 0m);
                    await RecordEmailDeliveryAsync(sent, "Payment receipt", memberName, member.Email, error);
                    TempData[sent ? "SuccessMessage" : "ErrorMessage"] = sent
                        ? $"Payment received, membership renewed through {newExpirationDate.Value:MMM d, yyyy}, and the digital receipt was emailed."
                        : $"Payment received and membership renewed through {newExpirationDate.Value:MMM d, yyyy}, but the digital receipt could not be emailed. {error}";
                }
            }

            return RedirectToAction(nameof(Payment));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordMemberPayment(int id, decimal amount)
        {
            if (amount <= 0)
            {
                TempData["ErrorMessage"] = "Payment amount must be greater than zero.";
                return RedirectToAction(nameof(Payment));
            }

            var member = await _context.Clients
                .AsNoTracking()
                .Where(client => client.UserId == id &&
                    client.Role == UserRole.Client)
                .Select(client => new
                {
                    client.Email,
                    client.MembershipPlan,
                    client.AssignedTrainerId,
                    client.PreferredDays,
                    client.PreferredTimeSlot,
                    FullName = (client.FirstName + " " + client.LastName).Trim()
                })
                .FirstOrDefaultAsync();

            if (member == null)
            {
                TempData["ErrorMessage"] = "The member could not be found.";
                return RedirectToAction(nameof(Payment));
            }

            var planPrice = MembershipPlanPricing.GetPrice(member.MembershipPlan);
            var previouslyPaid = await _context.Payments
                .AsNoTracking()
                .Where(payment => payment.ClientId == id && payment.Status == PaymentStatus.Paid)
                .SumAsync(payment => (decimal?)payment.Amount) ?? 0m;
            var balanceBeforePayment = Math.Max(0m, planPrice - previouslyPaid);
            if (balanceBeforePayment <= 0)
            {
                TempData["ErrorMessage"] = $"{member.FullName}'s {member.MembershipPlan} membership is already fully paid.";
                return RedirectToAction(nameof(Payment));
            }
            if (amount > balanceBeforePayment)
            {
                TempData["ErrorMessage"] = $"Payment cannot exceed the remaining balance of ₱{balanceBeforePayment:N2}.";
                return RedirectToAction(nameof(Payment));
            }

            var paidAt = DateTime.Now;
            var paidDayEnd = paidAt.Date.AddDays(1);
            var duplicateExists = await _context.Payments
                .AsNoTracking()
                .AnyAsync(payment => payment.ClientId == id &&
                    payment.Amount == amount &&
                    payment.Status == PaymentStatus.Paid &&
                    payment.PaymentDate >= paidAt.Date &&
                    payment.PaymentDate < paidDayEnd);

            if (duplicateExists)
            {
                TempData["ErrorMessage"] =
                    $"A cash payment of ₱{amount:N2} has already been recorded for {member.FullName} today.";
                return RedirectToAction(nameof(Payment));
            }

            var transactionCode =
                $"TXN-{paidAt:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            _context.Payments.Add(new Payment
            {
                ClientId = id,
                Amount = amount,
                Status = PaymentStatus.Paid,
                TransactionCode = transactionCode,
                PaymentDate = paidAt,
                DueDate = paidAt,
                Notes = "Cash payment recorded by Front Desk."
            });

            var totalPaid = previouslyPaid + amount;
            var remainingBalance = Math.Max(0m, planPrice - totalPaid);
            if (remainingBalance <= 0m)
            {
                var client = await _context.Clients.FirstAsync(item => item.UserId == id);
                if (client.Status != MemberStatus.Terminated && client.Status != MemberStatus.Canceled)
                {
                    client.Status = MemberStatus.Active;
                    client.IsActiveMember = true;
                    client.IsActive = true;
                    if (member.AssignedTrainerId.HasValue)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = member.AssignedTrainerId.Value,
                            Title = "New member assigned",
                            Message = $"{member.FullName} has completed membership payment and is assigned to you. Training schedule: {member.PreferredDays ?? "Days not set"} · {member.PreferredTimeSlot ?? "Time not set"}.",
                            NotificationType = "NewMemberAssigned",
                            CreatedDate = DateTime.Now
                        });
                    }
                }
            }
            await _context.SaveChangesAsync();
            if (string.IsNullOrWhiteSpace(member.Email))
            {
                await RecordEmailDeliveryAsync(
                    false,
                    "Payment receipt",
                    member.FullName,
                    "No email address",
                    "The member has no email address.");
                TempData["ErrorMessage"] =
                    $"Cash payment of ₱{amount:N2} was recorded. Remaining balance: ₱{remainingBalance:N2}. The member has no email address for the receipt.";
            }
            else
            {
                var (sent, error) = await _gmailService.SendMemberPaymentReceiptAsync(
                    member.Email,
                    member.FullName,
                    transactionCode,
                    amount,
                    paidAt,
                    MembershipPlanPricing.GetName(member.MembershipPlan),
                    planPrice,
                    totalPaid,
                    remainingBalance);

                await RecordEmailDeliveryAsync(
                    sent,
                    "Payment receipt",
                    member.FullName,
                    member.Email,
                    error);

                TempData[sent ? "SuccessMessage" : "ErrorMessage"] = sent
                    ? $"Cash payment of ₱{amount:N2} recorded. Remaining balance: ₱{remainingBalance:N2}. The receipt was emailed successfully."
                    : error;
            }
            return RedirectToAction(nameof(Payment));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMemberDetails(
            int id,
            int? assignedTrainerId,
            DateTime? followUpDate,
            string? followUpPurpose,
            List<string>? preferredDays,
            string? preferredTimeSlot)
        {
            var updated = await _memberService
                .UpdateAssignmentAndFollowUpAsync(
                    id,
                    assignedTrainerId,
                    followUpDate,
                    followUpPurpose,
                    preferredDays,
                    preferredTimeSlot);

            if (!updated)
            {
                TempData["ErrorMessage"] = "Member details could not be updated.";
                return RedirectToAction(nameof(Member));
            }

            if (followUpDate.HasValue && !string.IsNullOrWhiteSpace(followUpPurpose))
            {
                var member = await _context.Clients
                    .AsNoTracking()
                    .Where(client => client.UserId == id)
                    .Select(client => new
                    {
                        client.Email,
                        FullName = (client.FirstName + " " + client.LastName).Trim()
                    })
                    .FirstOrDefaultAsync();

                if (member == null || string.IsNullOrWhiteSpace(member.Email))
                {
                    await RecordEmailDeliveryAsync(
                        false,
                        "Follow-up letter",
                        member?.FullName ?? "Unknown member",
                        member?.Email ?? "No email address",
                        "No email address is available for this member.");
                    TempData["ErrorMessage"] =
                        "Member details were saved, but no email address is available for the follow-up.";
                }
                else
                {
                    var (sent, error) = await _gmailService.SendMemberFollowUpAsync(
                        member.Email,
                        member.FullName,
                        followUpDate.Value,
                        followUpPurpose);

                    await RecordEmailDeliveryAsync(
                        sent,
                        "Follow-up letter",
                        member.FullName,
                        member.Email,
                        error);

                    TempData[sent ? "SuccessMessage" : "ErrorMessage"] = sent
                        ? "Trainer assignment and follow-up saved. The generated follow-up letter was emailed to the member."
                        : error;
                }
            }
            else
            {
                TempData["SuccessMessage"] =
                    "Trainer assignment and follow-up updated successfully.";
            }

            return RedirectToAction(nameof(Member));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMembership(int id)
        {
            var member = await _context.Clients.AsNoTracking()
                .FirstOrDefaultAsync(client => client.UserId == id);
            var consumedDays = await _memberService
                .CancelMembershipAsync(id);

            if (!consumedDays.HasValue)
            {
                TempData["ErrorMessage"] = "Member was not found.";
            }
            else
            {
                var totalPaid = await _context.Payments.AsNoTracking()
                    .Where(payment => payment.ClientId == id && payment.Status == PaymentStatus.Paid)
                    .SumAsync(payment => (decimal?)payment.Amount) ?? 0m;
                var membershipEnd = member?.MembershipEndDate ?? (member == null
                    ? DateTime.Today
                    : member.MembershipStartDate.AddMonths(MembershipPlanPricing.GetDurationMonths(member.MembershipPlan)));
                var membershipDays = member == null
                    ? 1
                    : Math.Max(1, (membershipEnd.Date - member.MembershipStartDate.Date).Days);
                var dailyRate = member == null ? 0m : MembershipPlanPricing.GetPrice(member.MembershipPlan) / membershipDays;
                var estimatedRefund = Math.Max(0m, totalPaid - (dailyRate * consumedDays.Value));
                var (sent, error) = await _gmailService.SendCancellationReceiptAsync(
                    member!.Email, $"{member.FirstName} {member.LastName}".Trim(), MembershipPlanPricing.GetPrice(member.MembershipPlan),
                    totalPaid, membershipDays, consumedDays.Value);
                await RecordEmailDeliveryAsync(sent, "Cancellation refund breakdown", $"{member.FirstName} {member.LastName}".Trim(), member.Email, error);
                await RecordAdminActivityAsync(
                    "Membership canceled",
                    member == null ? $"Member #{id}" : $"{member.FirstName} {member.LastName}".Trim(),
                    $"Membership canceled with {consumedDays.Value} elapsed membership day(s) consumed.");
                TempData["SuccessMessage"] =
                    $"Membership canceled successfully. {consumedDays.Value} calendar membership day(s) consumed after confirmation. Estimated refund: ₱{estimatedRefund:N2}. " +
                    (sent ? "The refund breakdown was emailed." : error);
            }

            return RedirectToAction(nameof(Member));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TerminateMembership(int id)
        {
            await _memberService.TerminateMembershipAsync(id);

            TempData["SuccessMessage"] =
                "Membership terminated successfully.";

            return RedirectToAction(nameof(Member));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HardDeleteMember(int id)
        {
            await _memberService.HardDeleteClientAsync(id);

            TempData["SuccessMessage"] =
                "Member permanently deleted.";

            return RedirectToAction(nameof(Member));
        }

        private async Task<MembersPageViewModel>
            BuildMembersPageViewModelAsync(
                string? searchTerm,
                MemberStatus? selectedStatus,
                MembershipPlanType? selectedPlan,
                int page,
                int pageSize,
                bool openDrawer)
        {
            var listModel = await _memberService.GetMemberListAsync(
                searchTerm,
                selectedStatus,
                selectedPlan,
                page,
                pageSize);

            var addModel = await _memberService
                .BuildAddMemberViewModelAsync();

            return new MembersPageViewModel
            {
                ListViewModel = listModel,
                AddMemberViewModel = addModel,
                OpenDrawer = openDrawer
            };
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            return int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);
        }

        private async Task RecordAdminActivityAsync(string title, string memberName, string details)
        {
            if (!TryGetCurrentUserId(out var userId)) return;
            _context.FrontDeskEmailNotifications.Add(new FrontDeskEmailNotification
            {
                StaffId = userId,
                Title = title,
                Message = $"{memberName}: {details}",
                Status = "Activity",
                EmailType = "Membership",
                DeliveryKey = $"MembershipActivity:{DateTime.Now:yyyyMMddHHmmssfff}",
                CreatedDate = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        private async Task RecordEmailDeliveryAsync(
            bool sent,
            string emailType,
            string recipientName,
            string recipientEmail,
            string? error)
        {
            if (!TryGetCurrentUserId(out var userId)) return;

            var status = sent ? "Sent" : "Failed";
            var message = sent
                ? $"{emailType} was sent to {recipientName} ({recipientEmail})."
                : $"{emailType} for {recipientName} ({recipientEmail}) was not sent. {error ?? "Email delivery failed."}";

            _context.FrontDeskEmailNotifications.Add(new FrontDeskEmailNotification
            {
                StaffId = userId,
                Title = $"Email {status}: {emailType}",
                Message = message,
                Status = status,
                EmailType = emailType,
                DeliveryKey = string.Empty,
                CreatedDate = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }
    }
}
