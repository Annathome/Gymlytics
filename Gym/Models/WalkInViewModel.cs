using System.ComponentModel.DataAnnotations;

namespace Gym.Models;

public class WalkInRegistrationViewModel
{
    [Required] public int? PaymentTypeId { get; set; }
    [Required, StringLength(100), CompleteRegistrationName, Display(Name = "First name")] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100), CompleteRegistrationName, Display(Name = "Last name")] public string LastName { get; set; } = string.Empty;
    [Required, RegularExpression(@"^[0-9]{11}$", ErrorMessage = "Enter exactly 11 digits (0-9). Letters, spaces, and symbols are not allowed."), Display(Name = "Contact number")] public string ContactNumber { get; set; } = string.Empty;
    [Required(ErrorMessage = "Email is required."), EmailAddress, StringLength(100), Display(Name = "Email")] public string Email { get; set; } = string.Empty;
    [Required, StringLength(50), Display(Name = "Reason for visit")] public string ReasonForVisit { get; set; } = "Day Pass";
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
