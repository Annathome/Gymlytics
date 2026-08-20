namespace Gym.Models
{
    public enum UserRole
    {
        Admin,
        Trainer,
        Client
    }

    public enum SessionStatus
    {
        Scheduled,
        Confirmed,
        Completed,
        Cancelled,
        Full
    }

    public enum PaymentStatus
    {
        Paid,
        Pending,
        Overdue,
        Unpaid
    }

    public enum MemberStatus
    {
        Active,
        Inactive,
        Overdue,
        ExpiringSoon,
        Expired
    }

    public enum MembershipPlanType
    {
        Monthly,
        Quarterly,
        Annual
    }


}