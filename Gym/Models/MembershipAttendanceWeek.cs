namespace Gym.Models;

public static class MembershipAttendanceWeek
{
    public static DateTime? LatestCompletedStart(DateTime membershipStart, DateTime today)
    {
        var completedWeeks = (today.Date - membershipStart.Date).Days / 7;
        return completedWeeks < 1 ? null : membershipStart.Date.AddDays((completedWeeks - 1) * 7);
    }

    public static string DeliveryKey(int memberId, DateTime start) => $"MembershipWeeklyAttendance:{memberId}:{start:yyyyMMdd}";
}
