using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gym.Models;

public class WalkInPaymentType
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)"), Range(0.01, 1000000)] public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
}
