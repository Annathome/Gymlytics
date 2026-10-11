namespace Gym.Models;

public sealed class AdminEquipmentViewModel
{
    public string Tab { get; set; } = "equipment";
    public List<EquipmentInventoryRow> Inventory { get; set; } = new();
    public List<Equipment> Items { get; set; } = new();
    public Dictionary<string, int> AvailableByEquipment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<EquipmentNameCatalog> EquipmentNames { get; set; } = new();

    public string SearchTerm { get; set; } = string.Empty;
    public int TotalAssets { get; set; }
    public int Available { get; set; }
    public int InUse { get; set; }
    public int Maintenance { get; set; }
    public int OutOfService { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 8;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
