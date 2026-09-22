using System.Globalization;

namespace Gym.Models;

public sealed class AdminTrainerManagementViewModel
{
    public int SessionsToday { get; set; }
    public List<AdminTrainerAvailabilityRow> Trainers { get; set; } = new();
    public List<AdminTrainerMemberSchedule> MemberSchedules { get; set; } = new();
    public int AvailableNow => Trainers.Count(item => item.IsAvailable(DateTime.Now.DayOfWeek, DateTime.Now.TimeOfDay));
    public int ActiveBookings => Trainers.Sum(item => item.AssignedMembers);
    public int TotalCapacity => Trainers.Sum(item => item.WeeklyMemberLimit);
    public int AvailableMemberCapacity => Trainers.Sum(item => item.RemainingSlots);

    public List<AdminTrainerMemberSchedule> GetScheduledMembers(DayOfWeek day, TimeSpan slot) =>
        MemberSchedules.Where(item => item.IsScheduled(day, slot)).ToList();
}

public sealed class AdminTrainerMemberSchedule
{
    public int? TrainerId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string PreferredDays { get; set; } = string.Empty;
    public string PreferredTimeSlot { get; set; } = string.Empty;

    public bool IsScheduled(DayOfWeek day, TimeSpan slot)
    {
        var matchesDay = PreferredDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => string.Equals(value, day.ToString(), StringComparison.OrdinalIgnoreCase));
        if (!matchesDay) return false;

        var startText = PreferredTimeSlot.Replace('–', '-').Replace('—', '-')
            .Split('-', 2, StringSplitOptions.TrimEntries).FirstOrDefault();
        return DateTime.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var start) &&
            start.TimeOfDay >= slot && start.TimeOfDay < slot.Add(TimeSpan.FromHours(1));
    }
}

public sealed class AdminTrainerAvailabilityRow
{
    public int TrainerId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string AvailableDays { get; set; } = string.Empty;
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public int WeeklyMemberLimit { get; set; }
    public int AssignedMembers { get; set; }
    public int RemainingSlots => Math.Max(0, WeeklyMemberLimit - AssignedMembers);

    public bool IsAvailable(DayOfWeek day, TimeSpan hour)
    {
        if (!StartTime.HasValue || !EndTime.HasValue || hour < StartTime || hour >= EndTime) return false;
        return AvailableDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => string.Equals(value, day.ToString(), StringComparison.OrdinalIgnoreCase));
    }
}
