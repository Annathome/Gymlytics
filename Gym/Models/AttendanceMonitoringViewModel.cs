namespace Gym.Models;

public class AttendanceMonitoringViewModel
{
    public string SearchTerm { get; set; } = string.Empty;
    public List<AttendanceMemberItem> Members { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 7;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public class AttendanceMemberItem
{
    public int ClientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Membership { get; set; } = string.Empty;
    public DateTime? CheckInAt { get; set; }
    public string Status { get; set; } = "Not checked in";
    public bool IsEligibleForCheckIn { get; set; } = true;
    public bool CanCheckIn => !CheckInAt.HasValue && IsEligibleForCheckIn;
    public bool ReminderEmailFailed { get; set; }
}
