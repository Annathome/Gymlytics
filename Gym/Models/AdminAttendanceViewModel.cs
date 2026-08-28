namespace Gym.Models;

public sealed class AdminAttendanceViewModel
{
    public List<AttendanceMemberItem> Members { get; set; } = new();
    public string SearchTerm { get; set; } = string.Empty;
    public int MembersCheckedIn { get; set; }
    public int WalkInsToday { get; set; }
    public int CurrentlyInGym { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
