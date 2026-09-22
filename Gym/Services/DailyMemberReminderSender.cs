using System.Globalization;
using Gym.Data;
using Gym.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

namespace Gym.Services;

public sealed class DailyMemberReminderSender
{
    private readonly GymDbContext _context;
    private readonly GmailValidationService _email;
    private readonly ITimeLimitedDataProtector _protector;
    private readonly IConfiguration _configuration;

    public DailyMemberReminderSender(GymDbContext context, GmailValidationService email, IDataProtectionProvider protection, IConfiguration configuration)
    {
        _context = context;
        _email = email;
        _protector = protection.CreateProtector("Gym.TrainerSessionReminder.v1").ToTimeLimitedDataProtector();
        _configuration = configuration;
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
        if (date < member.MembershipStartDate.Date || (member.MembershipEndDate.HasValue && date > member.MembershipEndDate.Value.Date))
            return (false, "The member's membership does not cover this day.");
        if (string.IsNullOrWhiteSpace(member.Email)) return (false, "The member has no email address.");

        var dayNames = new[] { date.ToString("dddd", CultureInfo.InvariantCulture), date.ToString("ddd", CultureInfo.InvariantCulture) };
        var scheduledToday = (member.PreferredDays ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(day => dayNames.Contains(day, StringComparer.OrdinalIgnoreCase));
        if (!scheduledToday) return (false, "The member has no training scheduled for this day.");

        var parts = (member.PreferredTimeSlot ?? "").Replace('–', '-').Replace('—', '-').Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
            !DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            return (false, "The member has no valid training time.");
        if (date.Add(start.TimeOfDay) <= DateTime.Now) return (false, "Today's training time has already started.");
        string? attendUrl = null;
        string? absentUrl = null;
        var trainerName = "Self-guided";
        if (member.AssignedTrainerId.HasValue)
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"]?.TrimEnd('/');
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var site) || (site.Scheme != "https" && site.Scheme != "http"))
                return (false, "Configure AppSettings:BaseUrl for reminder response links.");
            var signature = $"{start:HHmm}-{end:HHmm}";
            var token = _protector.Protect($"{member.UserId}|{member.AssignedTrainerId.Value}|{date:yyyyMMdd}|{signature}", TimeSpan.FromHours(48));
            var link = $"{baseUrl}/FrontDesk/SessionReminderResponse?token={Uri.EscapeDataString(token)}";
            attendUrl = link + "&response=Confirmed";
            absentUrl = link + "&response=Cancelled";
            trainerName = await _context.Trainers.AsNoTracking()
                .Where(trainer => trainer.UserId == member.AssignedTrainerId.Value)
                .Select(trainer => (trainer.FirstName + " " + trainer.LastName).Trim())
                .FirstOrDefaultAsync(cancellationToken) ?? trainerName;
        }
        var fullName = $"{member.FirstName} {member.LastName}".Trim();
        var (sent, error) = await _email.SendDailyTrainingReminderAsync(member.Email, fullName, trainerName, date, member.PreferredTimeSlot ?? "Time not set", attendUrl, absentUrl);
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
