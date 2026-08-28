namespace Gym.Models;

public class FrontDeskPaymentViewModel
{
    public string SearchTerm { get; set; } = string.Empty;
    public PaymentStatus? SelectedStatus { get; set; }
    public List<FrontDeskPaymentItem> Payments { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public class FrontDeskPaymentItem
{
    public int PaymentId { get; set; }
    public int ClientId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;
    public string MembershipNumber { get; set; } = string.Empty;
    public MembershipPlanType Plan { get; set; }
    public decimal PlanPrice { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RemainingBalance { get; set; }
    public MemberStatus MemberStatus { get; set; }
    public DateTime? MembershipEndDate { get; set; }
    public string Initials { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
}
