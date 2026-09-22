using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gym.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required, StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required, Phone, StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        public string ProfileImageUrl { get; set; } = string.Empty;

        [StringLength(500)]
        public string Specialization { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public virtual ICollection<Session> TrainerSessions { get; set; } = new List<Session>();
        public virtual ICollection<ClientProgram> ClientPrograms { get; set; } = new List<ClientProgram>();
    }

    public class Client : User
    {
        public string MembershipNumber { get; set; } = string.Empty;
        public MembershipPlanType MembershipPlan { get; set; }
        public DateTime MembershipStartDate { get; set; }
        public DateTime? MembershipEndDate { get; set; }
        public MemberStatus Status { get; set; } = MemberStatus.Active;
        public bool IsActiveMember { get; set; } = true;

        // NEW: schedule/registration details captured on the intake form
        public int? AssignedTrainerId { get; set; }
        public GymGoal? GymGoal { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? FollowUpPurpose { get; set; }
        public string? PreferredDays { get; set; }      // comma-separated, e.g. "Mon,Wed,Fri"
        public string? PreferredTimeSlot { get; set; }
        public string? Notes { get; set; }

        [NotMapped]
        public int ClientId
        {
            get => UserId;
            set => UserId = value;
        }

        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class Trainer : User
    {
        [StringLength(500)]
        public string Bio { get; set; } = string.Empty;

        public int TotalClients { get; set; }
        public decimal AverageRating { get; set; }

        [NotMapped]
        public int TrainerId
        {
            get => UserId;
            set => UserId = value;
        }
    }

    public class TrainerAvailability
    {
        [Key] public int TrainerId { get; set; }
        [Required, StringLength(100)] public string AvailableDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";
        public TimeSpan StartTime { get; set; } = new(8, 0, 0);
        public TimeSpan EndTime { get; set; } = new(17, 0, 0);
        [Range(1, 100)] public int WeeklyMemberLimit { get; set; } = 10;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class Session
    {
        [Key]
        public int SessionId { get; set; }

        [Required]
        public int TrainerId { get; set; }

        [Required, StringLength(100)]
        public string SessionType { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [StringLength(100)]
        public string Location { get; set; } = string.Empty;

        [StringLength(50)]
        public string Room { get; set; } = string.Empty;

        public int MaxCapacity { get; set; }
        public int CurrentCapacity { get; set; }

        [Required]
        public SessionStatus Status { get; set; }

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [ForeignKey("TrainerId")]
        public virtual Trainer Trainer { get; set; } = null!;
        public virtual ICollection<SessionAttendee> Attendees { get; set; } = new List<SessionAttendee>();
    }

    public class ClientProgram
    {
        [Key]
        public int ProgramId { get; set; }

        [Required]
        public int ClientId { get; set; }

        [Required]
        public int TrainerId { get; set; }

        [Required, StringLength(100)]
        public string ProgramName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int SessionsAllowed { get; set; }
        public int SessionsCompleted { get; set; }
        public int SessionsRemaining { get; set; }
        public DateTime NextSessionDate { get; set; }
        public bool IsActive { get; set; } = true;

        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; } = null!;

        [ForeignKey("TrainerId")]
        public virtual Trainer Trainer { get; set; } = null!;
    }

    public class SessionAttendee
    {
        [Key]
        public int AttendeeId { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public int ClientId { get; set; }

        public bool IsConfirmed { get; set; }
        public bool IsAttended { get; set; }
        public DateTime BookedDate { get; set; } = DateTime.Now;

        [ForeignKey("SessionId")]
        public virtual Session Session { get; set; } = null!;

        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; } = null!;
    }

    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        public string NotificationType { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }

    public class FrontDeskEmailNotification
    {
        [Key] public int Id { get; set; }
        public int StaffId { get; set; }
        [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
        [Required, StringLength(1000)] public string Message { get; set; } = string.Empty;
        [Required, StringLength(20)] public string Status { get; set; } = "Sent";
        [Required, StringLength(80)] public string EmailType { get; set; } = string.Empty;
        [StringLength(200)] public string DeliveryKey { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    public class WalkInClient
    {
        [Key]
        public int WalkInClientId { get; set; }
        [StringLength(100)] public string? PaymentTypeName { get; set; }
        [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
        [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
        [Required, StringLength(20)] public string ContactNumber { get; set; } = string.Empty;
        [StringLength(100)] public string? Email { get; set; }
        [Required, StringLength(50)] public string ReasonForVisit { get; set; } = "Day Pass";
        [Required, StringLength(30)] public string PaymentMethod { get; set; } = "Cash";
        public decimal AmountPaid { get; set; }
        public bool IssueDigitalReceipt { get; set; }
        public bool LiabilityAccepted { get; set; }
        [Required, StringLength(20)] public string Status { get; set; } = "Checked-in";
        public DateTime RegisteredAt { get; set; } = DateTime.Now;
    }

    public class MemberAttendanceLog
    {
        [Key] public int AttendanceLogId { get; set; }
        public int ClientId { get; set; }
        public DateTime CheckInAt { get; set; }
        public DateTime? CheckOutAt { get; set; }
    }

    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        public int ClientId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public PaymentStatus Status { get; set; }

        [StringLength(50)]
        public string TransactionCode { get; set; } = string.Empty;

        public DateTime PaymentDate { get; set; }
        public DateTime DueDate { get; set; }

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; } = null!;
    }

    public class Equipment
    {
        [Key]
        public int EquipmentId { get; set; }

        [Required, StringLength(100)]
        public string EquipmentName { get; set; } = string.Empty;

        [StringLength(50)]
        public string Category { get; set; } = "General";

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public int AvailableQuantity { get; set; }
        public string Status { get; set; } = "Available";
        public DateTime AddedDate { get; set; } = DateTime.Now;
        public DateTime? PurchaseDate { get; set; }
        public DateTime? GymArrivalDate { get; set; }
        public DateTime? LastMaintenanceDate { get; set; }
        public DateTime? NextMaintenanceDate { get; set; }

        [StringLength(300)]
        public string? EvidencePhotoPath { get; set; }
    }

    public class EquipmentNameCatalog
    {
        [Key]
        public int EquipmentNameCatalogId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime PurchaseDate { get; set; }
        public DateTime GymArrivalDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class UpdateEquipmentRequest
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }

    public class MembershipPlanSetting
    {
        [Key]
        public MembershipPlanType Plan { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, 10000000)]
        public decimal Price { get; set; }

        [Range(1, 120)]
        public int DurationMonths { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
