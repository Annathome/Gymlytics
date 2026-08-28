namespace Gym.Models;

public class AttendanceMonitoringViewModel
{
    public string SearchTerm { get; set; } = string.Empty;
    public List<AttendanceMemberItem> Members { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
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
    // Retained for the separate Admin attendance workflow. Front Desk does not
    // display or accept check-out actions.
    public DateTime? CheckOutAt { get; set; }
    public string Status { get; set; } = "Not checked in";
    public bool CanCheckIn => !CheckInAt.HasValue;
    public bool CanCheckOut => Status == "Active";
}
