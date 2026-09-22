namespace Gym.Models;

public sealed class AdminPaymentsViewModel
{
    public List<WalkInPaymentType> WalkInPaymentTypes { get; set; } = new();
    public List<AdminPaymentTransaction> Payments { get; set; } = new();
    public string? SearchTerm { get; set; }
    public PaymentStatus? SelectedStatus { get; set; }
    public decimal MemberRevenueThisMonth { get; set; }
    public decimal WalkInRevenueThisMonth { get; set; }
    public int OverdueAccounts { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public List<MembershipPlanSetting> PlanSettings { get; set; } = new();
}

public sealed class AdminPaymentTransaction
{
    public string Name { get; set; } = string.Empty;
    public string TransactionCode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Method { get; set; } = "Cash";
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public PaymentStatus Status { get; set; }
    public int? MemberId { get; set; }
}
