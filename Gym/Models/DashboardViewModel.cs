using System;
using System.Collections.Generic;

namespace Gym.Models
{
    public class DashboardStatistics
    {
        public int TotalMembers { get; set; }
        public int ActiveMembers { get; set; }
        public int TodayAttendance { get; set; }
        public decimal PendingPayments { get; set; }
        public int EquipmentIssues { get; set; }
        public int ExpiringMembershipsCount { get; set; }
        public decimal TodayMemberRevenue { get; set; }
        public decimal TodayWalkInRevenue { get; set; }
        public int TodayMemberAttendance { get; set; }
        public int TodayWalkIns { get; set; }
        public int PendingWeeklyAttendanceReminders { get; set; }
        public int UnreadNotificationsCount { get; set; }
        public List<AdminEquipmentStatusItem> EquipmentStatus { get; set; } = new();

        public List<WeeklyAttendanceData> WeeklyAttendance { get; set; } = new List<WeeklyAttendanceData>();
        public List<RecentPayment> RecentPayments { get; set; } = new List<RecentPayment>();
        public List<ExpiringMembership> ExpiringMemberships { get; set; } = new List<ExpiringMembership>();
        public List<AdminTrainerListItem> Trainers { get; set; } = new();
    }

    public class AdminTrainerListItem
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class AdminEquipmentStatusItem
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class WeeklyAttendanceData
    {
        public string Day { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class RecentPayment
    {
        public int PaymentId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string TransactionCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; }
        public DateTime PaymentDate { get; set; }
    }

    public class ExpiringMembership
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public int DaysRemaining { get; set; }
    }

    public class FrontDeskDashboardViewModel
    {
        public int ExpiringSoonMemberships { get; set; }
        public int WeeklyWalkIns { get; set; }
        public int EquipmentNeedingMaintenance { get; set; }
        public int UnreadNotificationsCount { get; set; }
        public List<FrontDeskScheduleItem> Schedule { get; set; } = new();
        public int SchedulePage { get; set; } = 1;
        public int ScheduleTotalCount { get; set; }
        public int ScheduleTotalPages => Math.Max(1, (int)Math.Ceiling(ScheduleTotalCount / 5d));
        public List<FrontDeskWeeklyTrafficPoint> WeeklyTraffic { get; set; } = new();
        public List<FrontDeskTrainerAvailabilityItem> TrainerAvailability { get; set; } = new();
    }

    public class FrontDeskWeeklyTrafficPoint
    {
        public string Day { get; set; } = string.Empty;
        public int MemberAttendance { get; set; }
        public int WalkIns { get; set; }
    }

    public class FrontDeskTrainerAvailabilityItem
    {
        public string TrainerName { get; set; } = string.Empty;
        public string AvailableDays { get; set; } = "Not set";
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int AssignedMembers { get; set; }
        public int? WeeklyMemberLimit { get; set; }
        public int? RemainingSlots => WeeklyMemberLimit.HasValue
            ? Math.Max(0, WeeklyMemberLimit.Value - AssignedMembers)
            : null;
    }

    public class FrontDeskScheduleItem
    {
        public int SessionId { get; set; }
        public int ClientId { get; set; }
        public int TrainerId { get; set; }
        public string TrainerName { get; set; } = string.Empty;
        public string MemberName { get; set; } = "Unassigned";
        public DateTime Date { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public SessionStatus Status { get; set; }
        public string DisplayStatus { get; set; } = "Scheduled";
        public bool IsReminded { get; set; }
    }

    public class SessionReminderResponseViewModel
    {
        public string Response { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public string TrainerName { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
