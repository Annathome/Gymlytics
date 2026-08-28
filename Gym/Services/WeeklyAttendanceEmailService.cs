using Gym.Data;
using Gym.Models;
using Microsoft.EntityFrameworkCore;

namespace Gym.Services;

public sealed class WeeklyAttendanceEmailService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeeklyAttendanceEmailService> _logger;

    public WeeklyAttendanceEmailService(
        IServiceScopeFactory scopeFactory,
        ILogger<WeeklyAttendanceEmailService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendWeeklySummariesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Weekly member attendance email processing failed.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task SendWeeklySummariesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<GmailValidationService>();

        var today = DateTime.Today;
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var currentWeekStart = today.AddDays(-daysSinceMonday);
        var weekStart = currentWeekStart.AddDays(-7);
        var weekEndExclusive = currentWeekStart;
        var weekEnd = weekEndExclusive.AddDays(-1);

        var members = await context.Clients
            .AsNoTracking()
            .Where(client => client.IsActive && client.IsActiveMember)
            .Select(client => new
            {
                client.UserId,
                client.Email,
                FullName = (client.FirstName + " " + client.LastName).Trim()
            })
            .ToListAsync(cancellationToken);

        var attendance = await context.MemberAttendanceLogs
            .AsNoTracking()
            .Where(log => log.CheckInAt >= weekStart && log.CheckInAt < weekEndExclusive)
            .Select(log => new { log.ClientId, log.CheckInAt })
            .ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            var deliveryKey = $"WeeklyAttendance:{member.UserId}:{weekStart:yyyyMMdd}";
            var alreadySent = await context.FrontDeskEmailNotifications
                .AsNoTracking()
                .AnyAsync(item => item.DeliveryKey == deliveryKey && item.Status == "Sent", cancellationToken);
            if (alreadySent) continue;

            var checkInDates = attendance
                .Where(log => log.ClientId == member.UserId)
                .Select(log => log.CheckInAt.Date)
                .Distinct()
                .OrderBy(date => date)
                .ToList();

            var (sent, error) = string.IsNullOrWhiteSpace(member.Email)
                ? (false, "The member has no email address.")
                : await emailService.SendWeeklyAttendanceSummaryAsync(
                    member.Email,
                    member.FullName,
                    weekStart,
                    weekEnd,
                    checkInDates.Count,
                    checkInDates);

            var status = sent ? "Sent" : "Failed";
            context.FrontDeskEmailNotifications.Add(new FrontDeskEmailNotification
            {
                StaffId = 0,
                Title = $"Email {status}: Weekly attendance summary",
                EmailType = "Weekly attendance summary",
                Status = status,
                DeliveryKey = deliveryKey,
                Message = sent
                    ? $"A weekly attendance summary ({checkInDates.Count} attended day(s)) was automatically sent to {member.FullName} ({member.Email})."
                    : $"The weekly attendance summary for {member.FullName} ({member.Email}) was not sent. {error}",
                CreatedDate = DateTime.Now
            });
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
