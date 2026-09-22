using System.Globalization;

namespace Gym.Models;

public static class TrainerTimeSlots
{
    public static List<string> Build(TimeSpan start, TimeSpan end)
    {
        var slots = new List<string>();
        if (start < TimeSpan.Zero || end > TimeSpan.FromDays(1) || end <= start) return slots;
        var firstHour = TimeSpan.FromHours(Math.Ceiling(start.TotalHours));
        for (var cursor = firstHour; cursor + TimeSpan.FromHours(1) <= end; cursor += TimeSpan.FromHours(1))
        {
            var finish = cursor + TimeSpan.FromHours(1);
            slots.Add($"{DateTime.Today.Add(cursor).ToString("h:mm tt", CultureInfo.InvariantCulture)} - {DateTime.Today.Add(finish).ToString("h:mm tt", CultureInfo.InvariantCulture)}");
        }
        return slots;
    }
}
