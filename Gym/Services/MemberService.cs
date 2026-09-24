using Gym.Data;
using Gym.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Gym.Services
{
    public class MemberService : IMemberService
    {
        private readonly GymDbContext _context;
        private readonly SetupService _setupService;

        public MemberService(GymDbContext context, SetupService setupService)
        {
            _context = context;
            _setupService = setupService;
        }

        // =====================================================
        // MEMBER LIST
        // =====================================================

        public async Task<MemberListViewModel> GetMemberListAsync(
            string? searchTerm,
            MemberStatus? selectedStatus,
            MembershipPlanType? selectedPlan,
            int page = 1,
            int pageSize = 10)
        {
            await SyncRegisteredTrainersAsync();
            page = Math.Max(page, 1);
            pageSize = Math.Max(pageSize, 1);

            searchTerm = searchTerm?.Trim();

            var query = _context.Clients
                .AsNoTracking()
                .Where(client => client.Role == UserRole.Client)
                .AsQueryable();

            // Do not exclude inactive clients here. They must remain visible
            // so they can be reactivated or permanently deleted.

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(client =>
                    client.FirstName.Contains(searchTerm) ||
                    client.LastName.Contains(searchTerm) ||
                    (client.FirstName + " " + client.LastName)
                        .Contains(searchTerm) ||
                    client.Email.Contains(searchTerm) ||
                    client.PhoneNumber.Contains(searchTerm) ||
                    client.MembershipNumber.Contains(searchTerm) ||
                    client.UserId.ToString().Contains(searchTerm));
            }

            if (selectedPlan.HasValue)
            {
                query = query.Where(client =>
                    client.MembershipPlan == selectedPlan.Value);
            }

            var clients = await query
                .OrderBy(client => client.LastName)
                .ThenBy(client => client.FirstName)
                .ToListAsync();

            var trainerNames = await _context.Trainers
                .AsNoTracking()
                .Where(trainer => trainer.IsActive)
                .ToDictionaryAsync(
                    trainer => trainer.UserId,
                    trainer => $"{trainer.FirstName} {trainer.LastName}");
            var trainerOptions = await BuildTrainerOptionsAsync();

            var dailyReminderPrefix = $"DailyMemberReminder:%:{DateTime.Today:yyyyMMdd}";
            var dailyReminderKeys = await _context.FrontDeskEmailNotifications.AsNoTracking()
                .Where(item => item.Status == "Sent" && EF.Functions.Like(item.DeliveryKey, dailyReminderPrefix))
                .Select(item => item.DeliveryKey).ToListAsync();
            var dailyReminderMemberIds = dailyReminderKeys.Select(key => key.Split(':'))
                .Where(parts => parts.Length == 3 && int.TryParse(parts[1], out _))
                .Select(parts => int.Parse(parts[1])).ToHashSet();

            var members = clients
                .Select(client =>
                {
                    var expirationDate =
                        client.MembershipEndDate ??
                        CalculateExpirationDate(
                            client.MembershipStartDate,
                            client.MembershipPlan);

                    var displayStatus = DetermineMemberStatus(
                        client,
                        expirationDate);

                    string? trainerName = null;

                    if (client.AssignedTrainerId.HasValue)
                    {
                        trainerNames.TryGetValue(
                            client.AssignedTrainerId.Value,
                            out trainerName);
                    }

                    var schedule = BuildSchedule(
                        client.PreferredDays,
                        client.PreferredTimeSlot);
                    var planPrice = MembershipPlanPricing.GetPrice(client.MembershipPlan);
                    // Fully paid memberships carry only their current plan value, never historical payments.
                    var paidAmount = client.Status == MemberStatus.Canceled ? 0m : planPrice;
                    var remainingBalance = 0m;
                    var membershipDays = Math.Max(1, (expirationDate.Date - client.MembershipStartDate.Date).Days);
                    // Refunds are prorated by elapsed calendar days, not gym attendance.
                    // The start/confirmation date consumes zero days; each following
                    // calendar day consumes one membership day even if the member is absent.
                    var consumedTrainingDays = Math.Clamp(
                        (DateTime.Today - client.MembershipStartDate.Date).Days,
                        0,
                        membershipDays);
                    var dailyRate = planPrice / membershipDays;
                    var estimatedRefund = Math.Max(0m, paidAmount - (dailyRate * consumedTrainingDays));

                    return new MemberListItemViewModel
                    {
                        Id = client.UserId,
                        MembershipNumber = client.MembershipNumber,
                        FullName =
                            $"{client.FirstName} {client.LastName}".Trim(),
                        Email = client.Email,
                        PhoneNumber = client.PhoneNumber,
                        Plan = client.MembershipPlan,
                        PlanPrice = planPrice,
                        PaidAmount = paidAmount,
                        RemainingBalance = remainingBalance,
                        ConsumedTrainingDays = consumedTrainingDays,
                        EstimatedRefund = decimal.Round(estimatedRefund, 2, MidpointRounding.AwayFromZero),
                        JoinedDate = client.MembershipStartDate,
                        ExpirationDate = expirationDate,
                        Status = displayStatus,
                        TrainerName = trainerName,
                        AssignedTrainerId = client.AssignedTrainerId,
                        GymGoal = client.GymGoal,
                        FollowUpDate = client.FollowUpDate,
                        FollowUpPurpose = client.FollowUpPurpose,
                        PreferredDays = client.PreferredDays,
                        PreferredTimeSlot = client.PreferredTimeSlot,
                        Schedule = schedule
                        ,DailyReminderSent = dailyReminderMemberIds.Contains(client.UserId)
                    };
                })
                .ToList();

            if (selectedStatus.HasValue)
            {
                members = members
                    .Where(member =>
                        member.Status == selectedStatus.Value)
                    .ToList();
            }

            var totalCount = members.Count;

            var totalPages = totalCount == 0
                ? 1
                : (int)Math.Ceiling(
                    totalCount / (double)pageSize);

            if (page > totalPages)
            {
                page = totalPages;
            }

            var pageItems = members
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Counts are based on all clients, not only the current
            // search/filter results.
            var allClients = await _context.Clients
                .AsNoTracking()
                .Where(client => client.Role == UserRole.Client)
                .ToListAsync();

            var activeCount = allClients.Count(client =>
            {
                var expirationDate =
                    client.MembershipEndDate ??
                    CalculateExpirationDate(
                        client.MembershipStartDate,
                        client.MembershipPlan);

                return DetermineMemberStatus(
                    client,
                    expirationDate) == MemberStatus.Active;
            });

            var incomingRenewalCount = allClients.Count(client =>
            {
                var expirationDate = client.MembershipEndDate ??
                    CalculateExpirationDate(client.MembershipStartDate, client.MembershipPlan);
                return DetermineMemberStatus(client, expirationDate) == MemberStatus.ExpiringSoon;
            });

            return new MemberListViewModel
            {
                ActiveCount = activeCount,
                IncomingRenewalCount = incomingRenewalCount,
                SearchTerm = searchTerm ?? string.Empty,
                SelectedStatus = selectedStatus,
                SelectedPlan = selectedPlan,
                TrainerOptions = trainerOptions,

                Paged = new PagedResult<MemberListItemViewModel>
                {
                    Items = pageItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                }
            };
        }

        // =====================================================
        // BUILD ADD-MEMBER FORM
        // =====================================================

        public async Task<AddMemberViewModel>
            BuildAddMemberViewModelAsync(
                AddMemberViewModel? existing = null)
        {
            await SyncRegisteredTrainersAsync();
            var model = existing ?? new AddMemberViewModel();

            model.TrainerOptions = await BuildTrainerOptionsAsync(model.SelectedTrainerId);

            var trainerAvailability = await _context.TrainerAvailabilities
                .AsNoTracking()
                .ToListAsync();
            model.TrainerAvailableDays = trainerAvailability.ToDictionary(
                item => item.TrainerId,
                item => item.AvailableDays
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList());
            model.TrainerTimeSlots = trainerAvailability.ToDictionary(
                item => item.TrainerId,
                item => TrainerTimeSlots.Build(item.StartTime, item.EndTime));

            model.TrainerOptions.Insert(
                0,
                new SelectListItem
                {
                    Value = string.Empty,
                    Text = "No trainer (self-guided)",
                    Selected = !model.SelectedTrainerId.HasValue
                });

            model.GymGoalOptions = new List<SelectListItem>
            {
                new()
                {
                    Value = ((int)GymGoal.MuscleGain).ToString(),
                    Text = "Muscle Gain",
                    Selected = model.SelectedGymGoal == GymGoal.MuscleGain
                },
                new()
                {
                    Value = ((int)GymGoal.WeightLoss).ToString(),
                    Text = "Weight Loss",
                    Selected = model.SelectedGymGoal == GymGoal.WeightLoss
                },
                new()
                {
                    Value = ((int)GymGoal.Cardio).ToString(),
                    Text = "Cardio",
                    Selected = model.SelectedGymGoal == GymGoal.Cardio
                }
            };

            model.PlanOptions = (await _context.MembershipPlanSettings.AsNoTracking()
                .Where(item => item.IsActive).OrderBy(item => item.Plan).ToListAsync())
                .Select(item => new SelectListItem
                {
                    Value = ((int)item.Plan).ToString(),
                    Text = $"{item.Name} — ₱{item.Price:N2}",
                    Selected = model.SelectedPlanId == item.Plan
                })
                .ToList();

            model.TimeSlotOptions = new List<SelectListItem>
            {
                CreateOption(
                    "7:00 AM - 8:00 AM",
                    "7:00 AM – 8:00 AM",
                    model.SelectedTimeSlot),

                CreateOption(
                    "8:00 AM - 9:00 AM",
                    "8:00 AM – 9:00 AM",
                    model.SelectedTimeSlot),

                CreateOption(
                    "9:00 AM - 10:00 AM",
                    "9:00 AM – 10:00 AM",
                    model.SelectedTimeSlot),

                CreateOption(
                    "10:00 AM - 11:00 AM",
                    "10:00 AM – 11:00 AM",
                    model.SelectedTimeSlot),

                CreateOption(
                    "11:00 AM - 12:00 PM",
                    "11:00 AM – 12:00 PM",
                    model.SelectedTimeSlot),

                CreateOption(
                    "12:00 PM - 1:00 PM",
                    "12:00 PM – 1:00 PM",
                    model.SelectedTimeSlot),

                CreateOption(
                    "1:00 PM - 2:00 PM",
                    "1:00 PM – 2:00 PM",
                    model.SelectedTimeSlot),

                CreateOption(
                    "2:00 PM - 3:00 PM",
                    "2:00 PM – 3:00 PM",
                    model.SelectedTimeSlot),

                CreateOption(
                    "3:00 PM - 4:00 PM",
                    "3:00 PM – 4:00 PM",
                    model.SelectedTimeSlot),

                CreateOption(
                    "8:00 PM - 9:00 PM",
                    "8:00 PM – 9:00 PM",
                    model.SelectedTimeSlot)
            };

            return model;
        }

        private async Task SyncRegisteredTrainersAsync()
        {
            var registeredTrainers = await _setupService
                .GetActiveRegisteredTrainersAsync();

            if (registeredTrainers.Count == 0)
            {
                return;
            }

            var registeredEmails = registeredTrainers
                .Select(trainer => trainer.Email.Trim())
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .ToList();
            var existingEmails = await _context.Users
                .AsNoTracking()
                .Where(user => registeredEmails.Contains(user.Email))
                .Select(user => user.Email)
                .ToListAsync();
            var existingEmailSet = existingEmails
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var registered in registeredTrainers)
            {
                var email = registered.Email.Trim();
                if (string.IsNullOrWhiteSpace(email) || existingEmailSet.Contains(email))
                {
                    continue;
                }

                var nameParts = registered.FullName
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var firstName = nameParts.Length > 1
                    ? string.Join(" ", nameParts[..^1])
                    : registered.FullName.Trim();
                var lastName = nameParts.Length > 1
                    ? nameParts[^1]
                    : "Trainer";

                _context.Trainers.Add(new Trainer
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PhoneNumber = registered.PhoneNumber.Trim(),
                    Role = UserRole.Trainer,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                });
                existingEmailSet.Add(email);
            }

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
            }
        }

        // =====================================================
        // CREATE MEMBER
        // =====================================================

        public async Task<Client> CreateClientAsync(
            AddMemberViewModel form)
        {
            if (!RegistrationName.ValidFull(form.FullName))
                throw new InvalidOperationException("Enter the member's complete first and last name. Initials and N/A are not accepted.");
            form.FullName = RegistrationName.Normalize(form.FullName);
            var registeredNames = await _context.Clients.AsNoTracking()
                .Where(client => client.Role == UserRole.Client)
                .Select(client => client.FirstName + " " + client.LastName).ToListAsync();
            if (registeredNames.Any(name => RegistrationName.Same(name, form.FullName)))
                throw new InvalidOperationException("A member with this full name is already registered. Use the existing member record.");
            if (!await _context.MembershipPlanSettings.AnyAsync(item => item.IsActive))
                throw new InvalidOperationException("Member registration is unavailable. An administrator must add a membership plan first.");

            if (!form.SelectedPlanId.HasValue)
            {
                throw new InvalidOperationException(
                    "Membership plan is required.");
            }

            var normalizedEmail = form.Email.Trim();

            var emailAlreadyExists = await _context.Users
                .AnyAsync(user =>
                    user.Email == normalizedEmail);

            if (emailAlreadyExists)
            {
                throw new InvalidOperationException(
                    "This Gmail address is already registered. Please use a different account.");
            }

            if (form.SelectedTrainerId.HasValue)
            {
                var trainerExists = await _context.Trainers
                    .AnyAsync(trainer =>
                        trainer.UserId ==
                            form.SelectedTrainerId.Value &&
                        trainer.IsActive);

                if (!trainerExists)
                {
                    throw new InvalidOperationException(
                        "The selected trainer does not exist or is inactive.");
                }

                var availability = await _context.TrainerAvailabilities.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.TrainerId == form.SelectedTrainerId.Value);
                if (availability != null)
                {
                    var assignedCount = await _context.Clients.AsNoTracking()
                        .CountAsync(client => client.AssignedTrainerId == form.SelectedTrainerId.Value &&
                            client.IsActive && client.IsActiveMember);
                    if (assignedCount >= availability.WeeklyMemberLimit)
                    {
                        throw new InvalidOperationException(
                            "The selected trainer has reached their weekly member capacity.");
                    }
                }
            }
            if (!await _context.MembershipPlanSettings.AnyAsync(item => item.Plan == form.SelectedPlanId.Value && item.IsActive))
            {
                throw new InvalidOperationException("The selected membership plan is no longer available.");
            }

            var (firstName, lastName) = form.GetSplitName();

            var plan = form.SelectedPlanId.Value;
            var planPrice = MembershipPlanPricing.GetPrice(plan);
            var paymentAmount = form.PaymentAmount ?? 0m;
            if (paymentAmount <= 0m)
            {
                throw new InvalidOperationException("A cash payment is required to register the member.");
            }
            if (paymentAmount > planPrice)
            {
                throw new InvalidOperationException($"Payment cannot exceed the plan price of ₱{planPrice:N2}.");
            }
            if (paymentAmount < planPrice)
            {
                throw new InvalidOperationException($"Payment must cover the full monthly plan price of ₱{planPrice:N2}.");
            }

            var expirationDate = CalculateExpirationDate(
                form.StartDate,
                plan);

            if (form.SelectedTrainerId.HasValue)
            {
                var trainerAvailability = await _context.TrainerAvailabilities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.TrainerId == form.SelectedTrainerId.Value);
                if (trainerAvailability == null)
                {
                    throw new InvalidOperationException("The selected trainer has not configured training days yet.");
                }

                var availableDays = trainerAvailability.AvailableDays
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                if (!TrainerTimeSlots.Build(trainerAvailability.StartTime, trainerAvailability.EndTime)
                    .Contains(form.SelectedTimeSlot, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Select a time slot within the trainer's current availability.");
                }
                form.SelectedDays = form.SelectedDays
                    .Where(selectedDay => availableDays.Contains(selectedDay, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (form.SelectedDays.Count == 0)
                {
                    throw new InvalidOperationException("Select at least one of the trainer's available training days.");
                }
            }

            var client = new Client
            {
                FirstName = firstName,
                LastName = lastName,
                Email = normalizedEmail,
                PhoneNumber = form.ContactNumber.Trim(),
                Role = UserRole.Client,
                IsActive = true,
                CreatedDate = DateTime.Now,

                MembershipNumber =
                    await GenerateMembershipNumberAsync(),

                MembershipPlan = plan,
                MembershipStartDate = form.StartDate.Date,
                MembershipEndDate = expirationDate,
                Status = MemberStatus.Active,
                IsActiveMember = true,

                AssignedTrainerId =
                    form.SelectedTrainerId,

                GymGoal = form.SelectedGymGoal,

                PreferredDays =
                    form.SelectedDays is { Count: > 0 }
                        ? string.Join(
                            ",",
                            form.SelectedDays
                                .Where(day =>
                                    !string.IsNullOrWhiteSpace(day))
                                .Select(day => day.Trim())
                                .Distinct())
                        : null,

                PreferredTimeSlot =
                    NullIfWhiteSpace(form.SelectedTimeSlot)
            };

            _context.Clients.Add(client);
            var paidAt = DateTime.Now;
            client.Payments.Add(new Payment
            {
                Amount = paymentAmount,
                Status = PaymentStatus.Paid,
                TransactionCode = $"TXN-{paidAt:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                PaymentDate = paidAt,
                DueDate = paidAt,
                Notes = "Initial cash membership payment recorded during registration."
            });
            if (client.AssignedTrainerId.HasValue)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = client.AssignedTrainerId.Value,
                    Title = "New member assigned",
                    Message = $"{client.FirstName} {client.LastName} has been assigned to you. Training schedule: {client.PreferredDays ?? "Days not set"} · {client.PreferredTimeSlot ?? "Time not set"}.",
                    NotificationType = "NewMemberAssigned",
                    CreatedDate = DateTime.Now
                });
            }
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException
                {
                    Number: 2601 or 2627
                })
            {
                throw new InvalidOperationException(
                    "This Gmail address is already registered.", ex);
            }
            return client;
        }

        // =====================================================
        // MARK INACTIVE
        // =====================================================

        public async Task DeleteClientAsync(int id)
        {
            var client = await FindClientAsync(id);

            if (client == null)
            {
                return;
            }

            client.Status = MemberStatus.Inactive;
            client.IsActiveMember = false;
            client.IsActive = false;

            await _context.SaveChangesAsync();
        }

        // =====================================================
        // REACTIVATE
        // =====================================================

        public async Task ReactivateClientAsync(int id)
        {
            var client = await FindClientAsync(id);

            if (client == null)
            {
                return;
            }

            client.Status = MemberStatus.Active;
            client.IsActiveMember = true;
            client.IsActive = true;

            // If the old membership has expired, give the member a
            // new period beginning today using the existing plan.
            if (!client.MembershipEndDate.HasValue ||
                client.MembershipEndDate.Value.Date <
                    DateTime.Today)
            {
                client.MembershipStartDate = DateTime.Today;

                client.MembershipEndDate =
                    CalculateExpirationDate(
                        DateTime.Today,
                        client.MembershipPlan);
            }

            await _context.SaveChangesAsync();
        }

        // =====================================================
        // RENEW MEMBERSHIP
        // =====================================================

        public async Task<DateTime?> RenewMembershipAsync(int id)
        {
            var client = await FindClientAsync(id);

            if (client == null ||
                client.Status == MemberStatus.Terminated)
            {
                return null;
            }

            if (!MembershipRenewalRules.CanRenew(client.Status, client.MembershipEndDate, DateTime.Today))
                return null;
            var restart = client.Status == MemberStatus.Canceled ||
                !client.MembershipEndDate.HasValue || client.MembershipEndDate.Value.Date < DateTime.Today;
            var renewalStart = restart ? DateTime.Today : client.MembershipEndDate!.Value.Date;
            if (restart) client.MembershipStartDate = DateTime.Today;

            client.MembershipEndDate = CalculateExpirationDate(
                renewalStart,
                client.MembershipPlan);
            client.Status = MemberStatus.Active;
            client.IsActiveMember = true;
            client.IsActive = true;

            await _context.SaveChangesAsync();

            return client.MembershipEndDate;
        }

        public async Task<bool> UpdateAssignmentAndFollowUpAsync(
            int id,
            int? assignedTrainerId,
            DateTime? followUpDate,
            string? followUpPurpose,
            IEnumerable<string>? preferredDays,
            string? preferredTimeSlot)
        {
            var client = await FindClientAsync(id);

            if (client == null)
            {
                return false;
            }

            if (assignedTrainerId.HasValue)
            {
                var trainerExists = await _context.Trainers.AnyAsync(trainer =>
                    trainer.UserId == assignedTrainerId.Value &&
                    trainer.IsActive);

                if (!trainerExists)
                {
                    return false;
                }

                var availability = await _context.TrainerAvailabilities.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.TrainerId == assignedTrainerId.Value);
                if (availability != null)
                {
                    var assignedCount = await _context.Clients.AsNoTracking()
                        .CountAsync(item => item.AssignedTrainerId == assignedTrainerId.Value &&
                            item.UserId != id && item.IsActive && item.IsActiveMember);
                    if (assignedCount >= availability.WeeklyMemberLimit)
                    {
                        return false;
                    }
                }
            }

            var previousTrainerId = client.AssignedTrainerId;
            client.AssignedTrainerId = assignedTrainerId;
            client.FollowUpDate = followUpDate?.Date;
            client.FollowUpPurpose = NullIfWhiteSpace(followUpPurpose);
            client.PreferredDays = preferredDays == null
                ? null
                : string.Join(",", preferredDays
                    .Where(day => !string.IsNullOrWhiteSpace(day))
                    .Select(day => day.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            client.PreferredTimeSlot = NullIfWhiteSpace(preferredTimeSlot);

            if (assignedTrainerId.HasValue && assignedTrainerId != previousTrainerId)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = assignedTrainerId.Value,
                    Title = "New member assigned",
                    Message = $"{client.FirstName} {client.LastName} has been assigned to you. Training schedule: {client.PreferredDays ?? "Days not set"} · {client.PreferredTimeSlot ?? "Time not set"}.",
                    NotificationType = "NewMemberAssigned",
                    CreatedDate = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // =====================================================
        // CANCEL MEMBERSHIP
        // =====================================================

        public async Task<int?> CancelMembershipAsync(int id)
        {
            var client = await FindClientAsync(id);

            if (client == null || client.Status == MemberStatus.Canceled)
            {
                return null;
            }

            var membershipEnd = client.MembershipEndDate ??
                client.MembershipStartDate.AddMonths(
                    MembershipPlanPricing.GetDurationMonths(client.MembershipPlan));
            var membershipDays = Math.Max(
                1,
                (membershipEnd.Date - client.MembershipStartDate.Date).Days);
            var consumedDays = Math.Clamp(
                (DateTime.Today - client.MembershipStartDate.Date).Days,
                0,
                membershipDays);

            client.Status = MemberStatus.Canceled;
            client.IsActiveMember = false;

            // Keep the user account active because cancellation is
            // different from account termination.
            client.IsActive = true;

            await _context.SaveChangesAsync();

            return consumedDays;
        }

        // =====================================================
        // TERMINATE MEMBERSHIP
        // =====================================================

        public async Task TerminateMembershipAsync(int id)
        {
            var client = await FindClientAsync(id);

            if (client == null)
            {
                return;
            }

            client.Status = MemberStatus.Terminated;
            client.IsActiveMember = false;
            client.IsActive = false;

            await _context.SaveChangesAsync();
        }

        // =====================================================
        // PERMANENT DELETE
        // =====================================================

        public async Task HardDeleteClientAsync(int id)
        {
            var client = await FindClientAsync(id);

            if (client == null)
            {
                return;
            }

            // Remove dependent records that reference the client.
            // This prevents SQL foreign-key constraint errors.
            var payments = await _context.Payments
                .Where(payment => payment.ClientId == id)
                .ToListAsync();

            var programRecords = await _context.ClientPrograms
                .Where(program => program.ClientId == id)
                .ToListAsync();

            var attendanceRecords = await _context.SessionAttendees
                .Where(attendee => attendee.ClientId == id)
                .ToListAsync();

            var attendanceLogs = await _context.MemberAttendanceLogs
                .Where(log => log.ClientId == id)
                .ToListAsync();

            var notifications = await _context.Notifications
                .Where(notification => notification.UserId == id)
                .ToListAsync();

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Payments.RemoveRange(payments);
                _context.ClientPrograms.RemoveRange(programRecords);
                _context.SessionAttendees.RemoveRange(
                    attendanceRecords);
                _context.MemberAttendanceLogs.RemoveRange(attendanceLogs);
                _context.Notifications.RemoveRange(notifications);
                _context.Clients.Remove(client);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =====================================================
        // PRIVATE HELPERS
        // =====================================================

        private Task<Client?> FindClientAsync(int id)
        {
            return _context.Clients.FirstOrDefaultAsync(
                client =>
                    client.UserId == id &&
                    client.Role == UserRole.Client);
        }

        private async Task<List<SelectListItem>> BuildTrainerOptionsAsync(int? selectedTrainerId = null)
        {
            var trainers = await _context.Trainers.AsNoTracking()
                .Where(trainer => trainer.IsActive)
                .OrderBy(trainer => trainer.FirstName)
                .ThenBy(trainer => trainer.LastName)
                .ToListAsync();
            var trainerIds = trainers.Select(trainer => trainer.UserId).ToList();
            var availability = await _context.TrainerAvailabilities.AsNoTracking()
                .Where(item => trainerIds.Contains(item.TrainerId))
                .ToDictionaryAsync(item => item.TrainerId);
            var assignedCounts = await _context.Clients.AsNoTracking()
                .Where(client => client.AssignedTrainerId.HasValue &&
                    trainerIds.Contains(client.AssignedTrainerId.Value) && client.IsActive && client.IsActiveMember)
                .GroupBy(client => client.AssignedTrainerId!.Value)
                .Select(group => new { TrainerId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.TrainerId, item => item.Count);

            return trainers.Select(trainer =>
            {
                assignedCounts.TryGetValue(trainer.UserId, out var assigned);
                availability.TryGetValue(trainer.UserId, out var schedule);
                var scheduleText = schedule == null
                    ? "Availability not set"
                    : $"{string.Join(", ", schedule.AvailableDays.Split(',').Select(day => day[..Math.Min(3, day.Length)]))} · " +
                      $"{DateTime.Today.Add(schedule.StartTime):h:mm tt}–{DateTime.Today.Add(schedule.EndTime):h:mm tt} · " +
                      $"{assigned}/{schedule.WeeklyMemberLimit} members";
                var full = schedule != null && assigned >= schedule.WeeklyMemberLimit && selectedTrainerId != trainer.UserId;
                return new SelectListItem
                {
                    Value = trainer.UserId.ToString(),
                    Text = $"{trainer.FirstName} {trainer.LastName} — {scheduleText}{(full ? " (Full)" : string.Empty)}",
                    Selected = selectedTrainerId == trainer.UserId,
                    Disabled = full
                };
            }).ToList();
        }

        private static DateTime CalculateExpirationDate(
            DateTime startDate,
            MembershipPlanType plan)
        {
            return startDate.AddMonths(MembershipPlanPricing.GetDurationMonths(plan));
        }

        private static MemberStatus DetermineMemberStatus(
            Client client,
            DateTime expirationDate)
        {
            // Preserve explicit administrative statuses.
            if (client.Status == MemberStatus.Terminated)
            {
                return MemberStatus.Terminated;
            }

            if (client.Status == MemberStatus.Canceled)
            {
                return MemberStatus.Canceled;
            }

            if (!client.IsActiveMember ||
                client.Status == MemberStatus.Inactive)
            {
                return MemberStatus.Inactive;
            }

            if (client.Status == MemberStatus.Overdue)
            {
                return MemberStatus.Overdue;
            }

            if (expirationDate.Date < DateTime.Today)
            {
                return MemberStatus.Expired;
            }

            if (expirationDate.Date <=
                DateTime.Today.AddDays(7))
            {
                return MemberStatus.ExpiringSoon;
            }

            return MemberStatus.Active;
        }

        private static string? BuildSchedule(
            string? preferredDays,
            string? preferredTimeSlot)
        {
            var hasDays =
                !string.IsNullOrWhiteSpace(preferredDays);

            var hasTime =
                !string.IsNullOrWhiteSpace(preferredTimeSlot);

            if (hasDays && hasTime)
            {
                return $"{preferredDays} • {preferredTimeSlot}";
            }

            if (hasDays)
            {
                return preferredDays;
            }

            if (hasTime)
            {
                return preferredTimeSlot;
            }

            return null;
        }

        private async Task<string>
            GenerateMembershipNumberAsync()
        {
            string membershipNumber;

            do
            {
                membershipNumber =
                    $"MEM-{Guid.NewGuid():N}"[..12]
                        .ToUpperInvariant();
            }
            while (await _context.Clients.AnyAsync(
                client =>
                    client.MembershipNumber ==
                        membershipNumber));

            return membershipNumber;
        }

        private static SelectListItem CreateOption(
            string value,
            string text,
            string? selectedValue)
        {
            return new SelectListItem
            {
                Value = value,
                Text = text,
                Selected = string.Equals(
                    value,
                    selectedValue,
                    StringComparison.OrdinalIgnoreCase)
            };
        }

        private static string? NullIfWhiteSpace(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
