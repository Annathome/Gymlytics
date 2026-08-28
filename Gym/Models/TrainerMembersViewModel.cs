namespace Gym.Models;

public class TrainerMembersViewModel
{
    public string SearchTerm { get; set; } = string.Empty;
    public List<TrainerMemberItemViewModel> Members { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 8;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public class TrainerMemberItemViewModel
{
    public int ClientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public MembershipPlanType Plan { get; set; }
    public string GymGoal { get; set; } = "Goal not set";
    public string TrainingSchedule { get; set; } = "Not scheduled";
    public DateTime JoinedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public MemberStatus Status { get; set; }
}
