using System.Globalization;
using Gym.Data;
using Gym.Models;
using Microsoft.EntityFrameworkCore;

namespace Gym.Services;

public sealed class DailyMemberReminderSender
{
    private readonly GymDbContext _context;
    private readonly GmailValidationService _email;

    public DailyMemberReminderSender(GymDbContext context, GmailValidationService email)
    {
        _context = context;
        _email = email;
    }

    public async Task<(bool Success, string Message)> SendAsync(int clientId, DateTime date, CancellationToken cancellationToken = default)
    {
        date = date.Date;
        var key = $"DailyMemberReminder:{clientId}:{date:yyyyMMdd}";
        if (await _context.FrontDeskEmailNotifications.AsNoTracking()
            .AnyAsync(item => item.DeliveryKey == key && item.Status == "Sent", cancellationToken))
            return (true, "Daily reminder was already sent.");

        var member = await _context.Clients.AsNoTracking()
            .FirstOrDefaultAsync(client => client.UserId == clientId && client.IsActive && client.IsActiveMember, cancellationToken);
        if (member == null) return (false, "The active member could not be found.");
        if (string.IsNullOrWhiteSpace(member.Email)) return (false, "The member has no email address.");

        var dayNames = new[] { date.ToString("dddd", CultureInfo.InvariantCulture), date.ToString("ddd", CultureInfo.InvariantCulture) };
        var scheduledToday = (member.PreferredDays ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(day => dayNames.Contains(day, StringComparer.OrdinalIgnoreCase));
        if (!scheduledToday) return (false, "The member has no training scheduled for this day.");

        var trainerName = "Assigned trainer";
        if (member.AssignedTrainerId.HasValue)
        {
            trainerName = await _context.Trainers.AsNoTracking()
                .Where(trainer => trainer.UserId == member.AssignedTrainerId.Value)
                .Select(trainer => (trainer.FirstName + " " + trainer.LastName).Trim())
                .FirstOrDefaultAsync(cancellationToken) ?? trainerName;
        }
        var fullName = $"{member.FirstName} {member.LastName}".Trim();
        var (sent, error) = await _email.SendDailyTrainingReminderAsync(member.Email, fullName, trainerName, date, member.PreferredTimeSlot ?? "Time not set");
        _context.FrontDeskEmailNotifications.Add(new FrontDeskEmailNotification
        {
            StaffId = 0,
            Title = sent ? "Email Sent: Daily member reminder" : "Email Failed: Daily member reminder",
            EmailType = "Daily member reminder",
            Status = sent ? "Sent" : "Failed",
            DeliveryKey = key,
            Message = sent ? $"A daily training reminder was sent to {fullName} ({member.Email})." : $"Daily reminder for {fullName} failed. {error}",
            CreatedDate = DateTime.Now
        });
        await _context.SaveChangesAsync(cancellationToken);
        return sent ? (true, "Daily reminder sent successfully.") : (false, error ?? "The daily reminder could not be sent.");
    }
}
