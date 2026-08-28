namespace Gym.Models
{
    public enum UserRole
    {
        Admin = 0,
        Trainer = 1,
        Client = 2
    }

    public enum SessionStatus
    {
        Scheduled = 0,
        Confirmed = 1,
        Completed = 2,
        Cancelled = 3,
        Full = 4
    }

    public enum PaymentStatus
    {
        Paid = 0,
        Pending = 1,
        Overdue = 2,
        Unpaid = 3
    }

    public enum MemberStatus
    {
        Active = 0,
        Inactive = 1,
        Overdue = 2,
        ExpiringSoon = 3,
        Expired = 4,
        Canceled = 5,
        Terminated = 6,
        PendingPayment = 7
    }

    public enum MembershipPlanType
    {
        Monthly = 0,
        Quarterly = 1,
        Annual = 2
    }

    public enum GymGoal
    {
        MuscleGain = 0,
        WeightLoss = 1,
        Cardio = 2
    }
}
