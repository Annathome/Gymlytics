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

        public List<WeeklyAttendanceData> WeeklyAttendance { get; set; } = new List<WeeklyAttendanceData>();
        public List<RecentPayment> RecentPayments { get; set; } = new List<RecentPayment>();
        public List<ExpiringMembership> ExpiringMemberships { get; set; } = new List<ExpiringMembership>();
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
}