using System;

namespace Gym.Models
{
    public class UserRoleViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public DateTime? LastLoginAt { get; set; }
        public string? TodayAttendanceStatus { get; set; }
        public DateTime? TodayCheckedInAt { get; set; }

        // ---- Computed Display Helpers ----

        public string DisplayName => FullName;

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName)) return "?";
                var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return parts.Length switch
                {
                    0 => "?",
                    1 => parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant(),
                    _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
                };
            }
        }

        public string RoleLabel => Role switch
        {
            "MainAdmin" => "Super Admin",
            "Admin" => "Admin",
            "Trainer" => "Trainer",
            "Staff" => "Staff",
            _ => Role
        };

        public string RoleBadgeClass => Role switch
        {
            "MainAdmin" or "Admin" => "badge-super-admin",
            "Staff" => "badge-staff",
            "Trainer" => "badge-trainer",
            _ => "badge-default"
        };

        public string StatusBadgeClass => Status switch
        {
            "Pending" => "badge-pending",
            "Suspended" => "badge-suspended",
            _ => "badge-active"
        };

        public string Access => Status switch
        {
            "Suspended" => "No access (suspended)",
            "Pending" => "Awaiting confirmation",
            _ => Role switch
            {
                "MainAdmin" or "Admin" => "Full access",
                "Trainer" => "Trainer tools",
                _ => "Standard access"
            }
        };

        public string LastLoginDisplay
        {
            get
            {
                if (Status == "Pending") return "Never (pending)";
                if (LastLoginAt is null) return "Never";

                var span = DateTime.Now - LastLoginAt.Value;
                if (span.TotalMinutes < 1) return "Just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
                return LastLoginAt.Value.ToString("MMM d, yyyy");
            }
        }

        public bool IsMainAdmin => Role == "MainAdmin";
        public bool TracksStaffAttendance => Role is "Staff" or "Trainer";
        public string TodayAttendanceDisplay => !TracksStaffAttendance
            ? "Not tracked"
            : TodayAttendanceStatus switch
            {
                "Present" when TodayCheckedInAt.HasValue => $"Checked in {TodayCheckedInAt:hh:mm tt}",
                "Absent" => "Absent",
                _ => "No login today"
            };
    }

    public class AdminResetUserPasswordViewModel
    {
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
