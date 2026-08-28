namespace Gym.Models;

public sealed class UserAttendanceItem
{
    public int StaffId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime AttendanceDate { get; set; }
    public string AttendanceStatus { get; set; } = string.Empty;
    public DateTime? CheckedInAt { get; set; }
    public DateTime? MarkedAbsentAt { get; set; }
}
