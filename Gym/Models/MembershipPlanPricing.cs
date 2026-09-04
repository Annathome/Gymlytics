namespace Gym.Models;

public static class MembershipPlanPricing
{
    private static readonly object Sync = new();
    private static readonly Dictionary<MembershipPlanType, PlanDefinition> Plans = new()
    {
        [MembershipPlanType.Monthly] = new("Monthly", 1200m, 1, true)
    };

    public static decimal GetPrice(MembershipPlanType plan)
    {
        lock (Sync)
            return Plans.TryGetValue(plan, out var definition)
                ? definition.Price
                : throw new ArgumentOutOfRangeException(nameof(plan), plan, "Unsupported membership plan.");
    }

    public static void SetPrice(MembershipPlanType plan, decimal price)
    {
        if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
        lock (Sync)
        {
            var existing = Plans.TryGetValue(plan, out var definition)
                ? definition
                : new PlanDefinition(plan.ToString(), price, 1, true);
            Plans[plan] = existing with { Price = price };
        }
    }

    public static string GetName(MembershipPlanType plan)
    {
        lock (Sync) return Plans.TryGetValue(plan, out var value) ? value.Name : plan.ToString();
    }

    public static int GetDurationMonths(MembershipPlanType plan)
    {
        lock (Sync) return Plans.TryGetValue(plan, out var value) ? value.DurationMonths : 1;
    }

    public static bool IsActive(MembershipPlanType plan)
    {
        lock (Sync) return plan == MembershipPlanType.Monthly &&
            Plans.TryGetValue(plan, out var value) && value.IsActive;
    }

    public static IReadOnlyList<(MembershipPlanType Plan, string Name, decimal Price)> GetActivePlans()
    {
        lock (Sync) return Plans.Where(item =>
                item.Key == MembershipPlanType.Monthly && item.Value.IsActive)
            .OrderBy(item => (int)item.Key)
            .Select(item => (item.Key, item.Value.Name, item.Value.Price)).ToList();
    }

    public static void Configure(MembershipPlanType plan, string name, decimal price, int durationMonths, bool isActive)
    {
        lock (Sync) Plans[plan] = new PlanDefinition(name, price, durationMonths, isActive);
    }

    private sealed record PlanDefinition(string Name, decimal Price, int DurationMonths, bool IsActive);
}
