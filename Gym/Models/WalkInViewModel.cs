using System.ComponentModel.DataAnnotations;

namespace Gym.Models;

public class WalkInRegistrationViewModel
{
    [Required] public int? PaymentTypeId { get; set; }
    [Required, Display(Name = "First name")] public string FirstName { get; set; } = string.Empty;
    [Required, Display(Name = "Last name")] public string LastName { get; set; } = string.Empty;
    [Required, Phone, Display(Name = "Contact number")] public string ContactNumber { get; set; } = string.Empty;
    [Required(ErrorMessage = "Email is required."), EmailAddress, Display(Name = "Email")] public string Email { get; set; } = string.Empty;
    [Required, Display(Name = "Reason for visit")] public string ReasonForVisit { get; set; } = "Day Pass";
    [Range(0.01, 1000000, ErrorMessage = "Amount paid must be greater than zero.")]
    [Display(Name = "Amount paid")] public decimal AmountPaid { get; set; }
    [Display(Name = "Issue digital receipt")] public bool IssueDigitalReceipt { get; set; }
}

public class WalkInClientListItem
{
    public int WalkInClientId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string ReasonForVisit { get; set; } = string.Empty;
    public DateTime FirstRegisteredAt { get; set; }
    public DateTime LastVisitAt { get; set; }
    public int VisitCount { get; set; }
}
