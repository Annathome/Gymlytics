using System;

namespace Gym.Models
{
    public class ActivityLogEntry
    {
        public string ActionType { get; set; } = string.Empty; // "Login" | "Logout"
        public DateTime OccurredAt { get; set; }
    }
}
