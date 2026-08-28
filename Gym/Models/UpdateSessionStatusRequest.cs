namespace Gym.Models;

/// <summary>
/// Request model for updating a trainer session's status.
/// </summary>
public class UpdateSessionStatusRequest
{
    public SessionStatus Status { get; set; }
}
