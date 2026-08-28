using System.ComponentModel.DataAnnotations;

namespace Gym.Models;

public class TrainerAvailabilityViewModel
{
    public List<string> SelectedDays { get; set; } = new();

    [Required, DataType(DataType.Time)]
    public TimeSpan StartTime { get; set; } = new(8, 0, 0);

    [Required, DataType(DataType.Time)]
    public TimeSpan EndTime { get; set; } = new(17, 0, 0);

    [Range(1, 100, ErrorMessage = "Weekly member capacity must be between 1 and 100.")]
    public int WeeklyMemberLimit { get; set; } = 10;

    public int CurrentlyAssignedMembers { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}
