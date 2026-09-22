namespace Gym.Models;

public static class EquipmentStock
{
    public static bool IsInventory(Equipment item) => item.Category == "Inventory" || item.Status is "Good" or "Available" or "In Use";

    public static List<EquipmentInventoryRow> Summarize(IEnumerable<EquipmentNameCatalog> catalog, IEnumerable<Equipment> records)
    {
        var groups = records.ToLookup(item => item.EquipmentName, StringComparer.OrdinalIgnoreCase);
        return catalog.Select(name =>
        {
            var items = groups[name.Name];
            var total = items.Where(IsInventory).Sum(item => item.Quantity);
            var affected = items.Where(item => !IsInventory(item)).Sum(item => item.Quantity);
            return new EquipmentInventoryRow(name.Name, total, affected, Math.Max(0, total - affected), name.GymArrivalDate);
        }).ToList();
    }
}

public sealed record EquipmentInventoryRow(string Name, int Total, int Reported, int Available, DateTime BroughtIn);
