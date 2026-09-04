using Gym.Data;
using Gym.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gym.Services
{
    public class TrainerService : ITrainerService
    {
        private readonly GymDbContext _context;

        public TrainerService(GymDbContext context)
        {
            _context = context;
        }

        public async Task<TrainerDashboardViewModel> GetTrainerAssignedScheduleAsync(int trainerId, DateTime? date = null)
        {
            var targetDate = (date ?? DateTime.Today).Date;
            var weekStart = targetDate.AddDays(-(((int)targetDate.DayOfWeek + 6) % 7));
            var trainer = await _context.Trainers.AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == trainerId);
            var assignedMembers = await _context.Clients.AsNoTracking()
                .Where(client => client.AssignedTrainerId == trainerId && client.IsActiveMember && client.IsActive)
                .OrderBy(client => client.FirstName)
                .ThenBy(client => client.LastName)
                .ToListAsync();

            var assignedWeek = Enumerable.Range(0, 7).Select(offset =>
            {
                var dayDate = weekStart.AddDays(offset);
                var fullDay = dayDate.ToString("dddd");
                var shortDay = dayDate.ToString("ddd");
                var sessions = assignedMembers.Where(client =>
                    (client.PreferredDays ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Any(day => day.Equals(fullDay, StringComparison.OrdinalIgnoreCase) ||
                                    day.Equals(shortDay, StringComparison.OrdinalIgnoreCase)))
                    .Select(client => new TrainerAssignedSessionViewModel
                    {
                        ClientId = client.UserId,
                        MemberName = $"{client.FirstName} {client.LastName}".Trim(),
                        TimeSlot = string.IsNullOrWhiteSpace(client.PreferredTimeSlot) ? "Time not set" : client.PreferredTimeSlot,
                        GymGoal = client.GymGoal?.ToString() switch
                        {
                            "MuscleGain" => "Muscle Gain",
                            "WeightLoss" => "Weight Loss",
                            "Cardio" => "Cardio",
                            _ => "Goal not set"
                        }
                    }).OrderBy(session => session.TimeSlot).ThenBy(session => session.MemberName).ToList();
                return new TrainerDayScheduleViewModel { Date = dayDate, Sessions = sessions };
            }).ToList();

            var weeklySchedule = assignedWeek.ToDictionary(
                day => day.Date.ToString("ddd"),
                day => day.MemberCount);
            var selectedDayCount = assignedWeek
                .FirstOrDefault(day => day.Date == targetDate)?.MemberCount ?? 0;
            var unreadNotificationsCount = await _context.Notifications.AsNoTracking()
                .CountAsync(notification => notification.UserId == trainerId && !notification.IsRead);

            return new TrainerDashboardViewModel
            {
                TrainerId = trainerId,
                TrainerName = trainer == null ? "Trainer" : $"{trainer.FirstName} {trainer.LastName}".Trim(),
                SelectedDate = targetDate,
                WeekStart = weekStart,
                TotalClients = assignedMembers.Count,
                TodaysSessionCount = selectedDayCount,
                WeeklyMemberCount = assignedWeek.SelectMany(day => day.Sessions)
                    .Select(session => session.ClientId).Distinct().Count(),
                UnreadNotificationsCount = unreadNotificationsCount,
                AssignedWeek = assignedWeek,
                WeeklySchedule = weeklySchedule
            };
        }

        public async Task<TrainerDashboardViewModel> GetTrainerDashboardAsync(int trainerId, DateTime? date = null)
        {
            DateTime targetDate = date ?? DateTime.Today;
            var daysFromMonday = ((int)targetDate.DayOfWeek + 6) % 7;
            var weekStart = targetDate.Date.AddDays(-daysFromMonday);

            // Use context DbSet directly if mapped, fallback to Set<Trainer>() safely
            var trainer = await _context.Set<Trainer>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TrainerId == trainerId);

            var viewModel = new TrainerDashboardViewModel
            {
                TrainerId = trainerId,
                TrainerName = trainer != null ? $"{trainer.FirstName} {trainer.LastName}".Trim() : "Unknown Trainer",
                Specialization = trainer?.Specialization ?? string.Empty,
                ProfileImageUrl = trainer?.ProfileImageUrl ?? string.Empty,
                AverageRating = trainer?.AverageRating ?? 0m,
                SelectedDate = targetDate.Date,
                WeekStart = weekStart
            };

            var assignedMembers = await _context.Clients
                .AsNoTracking()
                .Where(client => client.AssignedTrainerId == trainerId &&
                    client.IsActiveMember && client.IsActive)
                .OrderBy(client => client.FirstName)
                .ThenBy(client => client.LastName)
                .ToListAsync();

            var assignedWeek = Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var dayDate = weekStart.AddDays(offset);
                    var dayName = dayDate.DayOfWeek.ToString();
                    var shortDayName = dayDate.ToString("ddd");
                    var sessions = assignedMembers
                        .Where(client => (client.PreferredDays ?? string.Empty)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Any(day => day.Equals(dayName, StringComparison.OrdinalIgnoreCase) ||
                                        day.Equals(shortDayName, StringComparison.OrdinalIgnoreCase)))
                        .Select(client => new TrainerAssignedSessionViewModel
                        {
                            ClientId = client.UserId,
                            MemberName = $"{client.FirstName} {client.LastName}".Trim(),
                            TimeSlot = string.IsNullOrWhiteSpace(client.PreferredTimeSlot) ? "Time not set" : client.PreferredTimeSlot,
                            GymGoal = client.GymGoal?.ToString() switch
                            {
                                "MuscleGain" => "Muscle Gain",
                                "WeightLoss" => "Weight Loss",
                                "Cardio" => "Cardio",
                                _ => "Goal not set"
                            }
                        })
                        .OrderBy(session => session.TimeSlot)
                        .ThenBy(session => session.MemberName)
                        .ToList();

                    return new TrainerDayScheduleViewModel { Date = dayDate, Sessions = sessions };
                })
                .ToList();

            viewModel.AssignedWeek = assignedWeek;
            viewModel.WeeklyMemberCount = assignedWeek.SelectMany(day => day.Sessions)
                .Select(session => session.ClientId).Distinct().Count();

            var allSessions = await _context.Sessions
                .Include(s => s.Attendees)
                    .ThenInclude(a => a.Client)
                .Where(s => s.TrainerId == trainerId)
                .AsNoTracking()
                .ToListAsync();

            var startOfDay = targetDate.Date;
            var endOfDay = targetDate.Date.AddDays(1).AddTicks(-1);

            var todaysSessionsList = allSessions
                .Where(s => s.StartTime >= startOfDay && s.StartTime <= endOfDay)
                .OrderBy(s => s.StartTime)
                .Select(MapToSessionViewModel)
                .ToList();

            var upcomingSessionsList = allSessions
                .Where(s => s.StartTime > endOfDay && s.Status != SessionStatus.Cancelled)
                .OrderBy(s => s.StartTime)
                .Select(MapToSessionViewModel)
                .ToList();

            var clientPrograms = await _context.ClientPrograms
                .Include(cp => cp.Client)
                .Where(cp => cp.TrainerId == trainerId && cp.IsActive)
                .AsNoTracking()
                .ToListAsync();

            // Null-safe mapping for Active Clients
            var activeClients = clientPrograms
                .Where(cp => cp.Client != null)
                .Select(cp => new ClientViewModel
                {
                    ClientId = cp.ClientId,
                    FullName = $"{cp.Client?.FirstName} {cp.Client?.LastName}".Trim(),
                    Email = cp.Client?.Email ?? string.Empty,
                    PhoneNumber = cp.Client?.PhoneNumber ?? string.Empty,
                    ProgramName = cp.ProgramName ?? string.Empty,
                    NextSessionDate = cp.NextSessionDate,
                    IsActive = cp.IsActive
                })
                .GroupBy(c => c.ClientId)
                .Select(g => g.First())
                .ToList();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == trainerId)
                .OrderByDescending(n => n.CreatedDate)
                .Take(10)
                .AsNoTracking()
                .ToListAsync();

            var notificationViewModels = notifications.Select(n => new NotificationViewModel
            {
                NotificationId = n.NotificationId,
                Title = n.Title ?? string.Empty,
                Message = n.Message ?? string.Empty,
                NotificationType = n.NotificationType ?? string.Empty,
                IsRead = n.IsRead,
                CreatedDate = n.CreatedDate
            }).ToList();

            // Safe dictionary key building
            var weeklySchedule = new Dictionary<string, int>();
            for (int i = 0; i < 7; i++)
            {
                var currentDay = targetDate.Date.AddDays(i);
                var dayName = currentDay.ToString("ddd");

                int count = allSessions.Count(s =>
                    s.StartTime.Date == currentDay &&
                    s.Status != SessionStatus.Cancelled);

                weeklySchedule[dayName] = count;
            }

            viewModel.TotalClients = assignedMembers.Count;
            viewModel.TodaysSessionCount = assignedWeek.First(day => day.Date == targetDate.Date).Sessions.Count;
            viewModel.UpcomingSessionCount = upcomingSessionsList.Count;
            viewModel.UnreadNotificationsCount = notifications.Count(n => !n.IsRead);

            viewModel.TodaysSessions = todaysSessionsList;
            viewModel.UpcomingSessions = upcomingSessionsList;
            viewModel.ActiveClients = activeClients;
            viewModel.RecentNotifications = notificationViewModels;
            viewModel.WeeklySchedule = weeklySchedule;

            return viewModel;
        }

        public async Task<List<Session>> GetTrainerSessionsAsync(int trainerId, DateTime date)
        {
            return await _context.Sessions
                .Where(s => s.TrainerId == trainerId && s.StartTime.Date == date.Date)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Session>> GetWeeklySessionsAsync(int trainerId)
        {
            var start = DateTime.Today;
            var end = start.AddDays(7);
            return await _context.Sessions
                .Where(s => s.TrainerId == trainerId && s.StartTime >= start && s.StartTime <= end)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<ClientViewModel>> GetTrainerClientsAsync(int trainerId)
        {
            var clientPrograms = await _context.ClientPrograms
                .Include(cp => cp.Client)
                .Where(cp => cp.TrainerId == trainerId)
                .AsNoTracking()
                .ToListAsync();

            return clientPrograms
                .Where(cp => cp.Client != null)
                .Select(cp => new ClientViewModel
                {
                    ClientId = cp.ClientId,
                    FullName = $"{cp.Client?.FirstName} {cp.Client?.LastName}".Trim(),
                    Email = cp.Client?.Email ?? string.Empty,
                    PhoneNumber = cp.Client?.PhoneNumber ?? string.Empty,
                    ProgramName = cp.ProgramName ?? string.Empty,
                    NextSessionDate = cp.NextSessionDate,
                    IsActive = cp.IsActive
                })
                .GroupBy(c => c.ClientId)
                .Select(g => g.First())
                .ToList();
        }

        public async Task<ClientProgram> GetClientProgramAsync(int programId)
        {
            return await _context.ClientPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.ProgramId == programId)
                ?? throw new KeyNotFoundException($"Program with ID {programId} was not found.");
        }

        public async Task<bool> UpdateSessionStatusAsync(int sessionId, SessionStatus status)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null) return false;

            session.Status = status;
            _context.Sessions.Update(session);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> BookSessionAsync(int sessionId, int clientId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null || session.CurrentCapacity >= session.MaxCapacity) return false;

            _context.Set<SessionAttendee>().Add(new SessionAttendee
            {
                SessionId = sessionId,
                ClientId = clientId,
                IsConfirmed = true,
                BookedDate = DateTime.Now
            });

            session.CurrentCapacity++;
            if (session.CurrentCapacity >= session.MaxCapacity)
            {
                session.Status = SessionStatus.Full;
            }

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> CancelSessionAsync(int sessionId, int clientId)
        {
            var attendee = await _context.Set<SessionAttendee>()
                .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.ClientId == clientId);

            if (attendee == null) return false;

            _context.Set<SessionAttendee>().Remove(attendee);

            var session = await _context.Sessions.FindAsync(sessionId);
            if (session != null)
            {
                session.CurrentCapacity = Math.Max(0, session.CurrentCapacity - 1);
                if (session.Status == SessionStatus.Full)
                {
                    session.Status = SessionStatus.Scheduled;
                }
            }

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<IEnumerable<NotificationViewModel>> GetTrainerNotificationsAsync(int trainerId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == trainerId)
                .OrderByDescending(n => n.CreatedDate)
                .Select(n => new NotificationViewModel
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title ?? string.Empty,
                    Message = n.Message ?? string.Empty,
                    NotificationType = n.NotificationType ?? string.Empty,
                    IsRead = n.IsRead,
                    CreatedDate = n.CreatedDate
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetUnreadNotificationCountAsync(int trainerId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == trainerId && !n.IsRead);
        }

        private static SessionViewModel MapToSessionViewModel(Session session)
        {
            return new SessionViewModel
            {
                SessionId = session.SessionId,
                TrainerId = session.TrainerId,
                SessionType = session.SessionType ?? string.Empty,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Location = session.Location ?? string.Empty,
                Room = session.Room ?? string.Empty,
                MaxCapacity = session.MaxCapacity,
                CurrentCapacity = session.CurrentCapacity,
                Status = session.Status,
                Description = session.Description ?? string.Empty,
                Attendees = session.Attendees?.Select(a => new ClientViewModel
                {
                    ClientId = a.ClientId,
                    FullName = a.Client != null ? $"{a.Client.FirstName} {a.Client.LastName}".Trim() : "Unknown Client",
                    Email = a.Client?.Email ?? string.Empty,
                    PhoneNumber = a.Client?.PhoneNumber ?? string.Empty,
                    IsActive = true
                }).ToList() ?? new List<ClientViewModel>()
            };
        }
    }
}
