using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Gym.Models;

public class FrontDeskEquipmentViewModel
{
    public string SearchTerm { get; set; } = string.Empty;
    public List<Equipment> Items { get; set; } = new();
    public List<EquipmentNameCatalog> EquipmentNames { get; set; } = new();
    public int TotalAssets { get; set; }
    public int InMaintenance { get; set; }
    public int OutOfOrder { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public class AddEquipmentViewModel
{
    [Required, StringLength(100)] public string EquipmentName { get; set; } = string.Empty;
    [StringLength(50)] public string Category { get; set; } = "General";
    [Range(1, 10000)] public int Quantity { get; set; } = 1;
    [Required] public string Condition { get; set; } = "Good";
    public IFormFile? EvidencePhoto { get; set; }
}

public class AddEquipmentNameViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required, DataType(DataType.Date)] public DateTime? GymArrivalDate { get; set; }
}
