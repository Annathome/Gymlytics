using System;
using System.Collections.Generic;

namespace Gym.Models
{

    // ==========================================
    // STANDALONE VIEW MODELS
    // ==========================================

    public class SessionViewModel
    {
        public int SessionId { get; set; }
        public int TrainerId { get; set; }
        public string TrainerName { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public int MaxCapacity { get; set; }
        public int CurrentCapacity { get; set; }
        public SessionStatus Status { get; set; }
        public string Description { get; set; } = string.Empty;

        // Session Attendees List
        public IEnumerable<ClientViewModel> Attendees { get; set; } = new List<ClientViewModel>();
    }

    public class ClientViewModel
    {
        public int ClientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public DateTime? NextSessionDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class NotificationViewModel
    {
        public int NotificationId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    // ==========================================
    // TRAINER DASHBOARD VIEW MODEL
    // ==========================================

    public class TrainerDashboardViewModel
    {
        // Trainer Profile Info
        public int TrainerId { get; set; }
        public string TrainerName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;

        // Metric Counters
        public int TotalClients { get; set; }
        public decimal AverageRating { get; set; }
        public int TodaysSessionCount { get; set; }
        public int UpcomingSessionCount { get; set; }
        public int UnreadNotificationsCount { get; set; }

        // Dashboard Data Collections
        public IEnumerable<SessionViewModel> TodaysSessions { get; set; } = new List<SessionViewModel>();
        public IEnumerable<SessionViewModel> UpcomingSessions { get; set; } = new List<SessionViewModel>();
        public IEnumerable<ClientViewModel> ActiveClients { get; set; } = new List<ClientViewModel>();
        public IEnumerable<NotificationViewModel> RecentNotifications { get; set; } = new List<NotificationViewModel>();

        // Weekly Schedule Summary (e.g., {"Mon": 3, "Tue": 5})
        public Dictionary<string, int> WeeklySchedule { get; set; } = new Dictionary<string, int>();
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public DateTime WeekStart { get; set; } = DateTime.Today;
        public int WeeklyMemberCount { get; set; }
        public List<TrainerDayScheduleViewModel> AssignedWeek { get; set; } = new();
    }

    public class TrainerDayScheduleViewModel
    {
        public DateTime Date { get; set; }
        public List<TrainerAssignedSessionViewModel> Sessions { get; set; } = new();
        public int MemberCount => Sessions
            .Where(session => session.ClientId > 0 && !string.IsNullOrWhiteSpace(session.MemberName))
            .Select(session => session.ClientId)
            .Distinct()
            .Count();
    }

    public class TrainerAssignedSessionViewModel
    {
        public DateTime? GetEndTime(DateTime date)
        {
            var parts = TimeSlot.Replace('–', '-').Replace('—', '-').Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !DateTime.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.NoCurrentDateDefault, out var end)) return null;
            return date.Date.Add(end.TimeOfDay);
        }

        public int ClientId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string TimeSlot { get; set; } = string.Empty;
        public string GymGoal { get; set; } = string.Empty;
    }
}
