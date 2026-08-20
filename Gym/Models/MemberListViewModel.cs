using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Gym.Models
{
    public class MembersPageViewModel
    {
        public MemberListViewModel ListViewModel { get; set; } = new();

        public AddMemberViewModel AddMemberViewModel { get; set; } = new();

        public bool OpenDrawer { get; set; } = false;
    }


    public class MemberListItemViewModel
    {
        public int Id { get; set; }

        public string MembershipNumber { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName))
                    return "??";

                var parts = FullName
                    .Trim()
                    .Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 1)
                    return parts[0][0]
                        .ToString()
                        .ToUpper();

                return $"{parts[0][0]}{parts[^1][0]}"
                    .ToUpper();
            }
        }

        public MembershipPlanType Plan { get; set; }

        public DateTime JoinedDate { get; set; }

        public DateTime ExpirationDate { get; set; }

        public MemberStatus Status { get; set; }

        public string? TrainerName { get; set; }

        public string? Schedule { get; set; }
    }


    public class MemberListViewModel
    {
        public int ActiveCount { get; set; }

        public int PendingPaymentCount { get; set; }


        // Search and filters

        public string SearchTerm { get; set; } = string.Empty;

        public MemberStatus? SelectedStatus { get; set; }

        public MembershipPlanType? SelectedPlan { get; set; }


        // Members

        public PagedResult<MemberListItemViewModel> Paged { get; set; } = new();


        // Convenience properties

        public List<MemberListItemViewModel> Members => Paged.Items;

        public int TotalCount => Paged.TotalCount;

        public int Page => Paged.Page;

        public int PageSize => Paged.PageSize;

        public int TotalPages => Paged.TotalPages;

        public bool HasPrevious => Paged.HasPrevious;

        public bool HasNext => Paged.HasNext;
    }


    public class AddMemberViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;


        [Required(ErrorMessage = "Date of birth is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }


        [Required(ErrorMessage = "Contact number is required.")]
        [Display(Name = "Contact number")]
        [Phone]
        public string ContactNumber { get; set; } = string.Empty;


        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;


        [Display(Name = "Emergency contact")]
        public string? EmergencyContact { get; set; }


        [Required(ErrorMessage = "Please select a plan.")]
        [Display(Name = "Plan type")]
        public MembershipPlanType? SelectedPlanId { get; set; }


        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime StartDate { get; set; } = DateTime.Today;


        [Display(Name = "Assign trainer")]
        public int? SelectedTrainerId { get; set; }


        [Display(Name = "Training days")]
        public List<string> SelectedDays { get; set; } = new();


        [Display(Name = "Preferred time slot")]
        public string? SelectedTimeSlot { get; set; }


        [DataType(DataType.Date)]
        [Display(Name = "Client call date")]
        public DateTime? CallDate { get; set; }


        [Display(Name = "Call purpose")]
        public string CallPurpose { get; set; }
            = "Welcome & orientation";


        [Display(Name = "Notes")]
        public string? Notes { get; set; }


        // Dropdown options

        public List<SelectListItem> PlanOptions { get; set; }
            = new();

        public List<SelectListItem> TrainerOptions { get; set; }
            = new();

        public List<SelectListItem> TimeSlotOptions { get; set; }
            = new();

        public List<SelectListItem> PurposeOptions { get; set; }
            = new();


        // Helper to extract First Name and Last Name

        public (string FirstName, string LastName) GetSplitName()
        {
            if (string.IsNullOrWhiteSpace(FullName))
                return ("Member", "User");

            var parts = FullName
                .Trim()
                .Split(
                    ' ',
                    2,
                    StringSplitOptions.RemoveEmptyEntries);

            return parts.Length switch
            {
                1 => (parts[0], "N/A"),

                _ => (parts[0], parts[1])
            };
        }

        public class MembersPageViewModel
        {
            public MemberListViewModel ListViewModel { get; set; } = new();
            public AddMemberViewModel AddMemberViewModel { get; set; } = new();
            public bool OpenDrawer { get; set; } = false;
        }
    }
}
