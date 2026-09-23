namespace Gym.Models;

public static class MembershipRenewalRules
{
    public static bool CanRenew(MemberStatus status, DateTime? endDate, DateTime today) =>
        status != MemberStatus.Terminated &&
        (status == MemberStatus.Canceled ||
         (endDate.HasValue && endDate.Value.Date <= today.Date.AddDays(7)));
}
