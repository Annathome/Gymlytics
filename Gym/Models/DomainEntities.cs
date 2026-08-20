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

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public int AvailableQuantity { get; set; }
        public string Status { get; set; } = "Available";
        public DateTime? LastMaintenanceDate { get; set; }
        public DateTime? NextMaintenanceDate { get; set; }
    }

    public class UpdateEquipmentRequest
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }
}