using System.ComponentModel.DataAnnotations;
namespace Gym.Models;
public class PasswordRecoveryModel
{
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? Token { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
}
