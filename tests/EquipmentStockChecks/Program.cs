using Gym.Models;

var catalog = new[] { new EquipmentNameCatalog { Name = "Treadmill" }, new EquipmentNameCatalog { Name = "Bike" } };
var records = new List<Equipment>
{
    new() { EquipmentName = "Treadmill", Category = "Inventory", Status = "Available", Quantity = 10 },
    new() { EquipmentName = "Treadmill", Status = "Needs Maintenance", Quantity = 3 },
    new() { EquipmentName = "Treadmill", Status = "Out of Order", Quantity = 2 }
};
void Check(int total, int reported, int available)
{
    var row = EquipmentStock.Summarize(catalog, records).Single(item => item.Name == "Treadmill");
    if ((row.Total, row.Reported, row.Available) != (total, reported, available))
        throw new Exception($"Unexpected stock: {row}");
}
Check(10, 5, 5);
records[1].Status = "Out of Order";
Check(10, 5, 5); // Changing issue status must not deduct the same units twice.
records.Add(new() { EquipmentName = "treadmill", Category = "Inventory", Status = "Available", Quantity = 4 });
Check(14, 5, 9); // Restocking adds stock and matching is case insensitive.
records.Add(new() { EquipmentName = "Treadmill", Status = "Needs Maintenance", Quantity = 9 });
Check(14, 14, 0);
records.Add(new() { EquipmentName = "Treadmill", Status = "Fair", Quantity = 1 });
Check(14, 15, 0); // Historical excess reports cannot produce negative availability.
var empty = EquipmentStock.Summarize(catalog, records).Single(item => item.Name == "Bike");
if (empty.Total != 0 || empty.Available != 0) throw new Exception("Empty catalog item has stock.");
if (!EquipmentStock.IsInventory(new Equipment { Status = "Good" })) throw new Exception("Legacy healthy stock lost.");
Console.WriteLine("PASS: deductions, status changes, restocking, zero stock, legacy data, and empty inventory.");

var trainerSlots = TrainerTimeSlots.Build(new TimeSpan(16, 0, 0), new TimeSpan(20, 0, 0));
if (trainerSlots.Count != 4 || trainerSlots[0] != "4:00 PM - 5:00 PM" || trainerSlots[^1] != "7:00 PM - 8:00 PM")
    throw new Exception("Trainer availability is not fully covered.");
var partialSlots = TrainerTimeSlots.Build(new TimeSpan(8, 30, 0), new TimeSpan(10, 0, 0));
if (partialSlots.Count != 1 || partialSlots[0] != "9:00 AM - 10:00 AM")
    throw new Exception("Partial-hour availability exceeded.");
if (TrainerTimeSlots.Build(TimeSpan.FromHours(17), TimeSpan.FromHours(8)).Count != 0)
    throw new Exception("Invalid availability accepted.");
Console.WriteLine("PASS: trainer time ranges, partial hours, and invalid availability.");

var joined = new DateTime(2026, 9, 20);
if (MembershipAttendanceWeek.LatestCompletedStart(joined, joined.AddDays(6)) != null)
    throw new Exception("Weekly summary is eligible too early.");
if (MembershipAttendanceWeek.LatestCompletedStart(joined, joined.AddDays(7)) != joined ||
    MembershipAttendanceWeek.LatestCompletedStart(joined, joined.AddDays(13)) != joined ||
    MembershipAttendanceWeek.LatestCompletedStart(joined, joined.AddDays(14)) != joined.AddDays(7))
    throw new Exception("Membership week boundaries are incorrect.");
if (MembershipAttendanceWeek.LatestCompletedStart(joined, joined.AddDays(-1)) != null)
    throw new Exception("Future membership received a summary.");
Console.WriteLine("PASS: weekly summaries wait seven days and use completed membership weeks.");
