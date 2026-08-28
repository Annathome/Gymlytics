using Gym.Data;
using Microsoft.EntityFrameworkCore;

namespace Gym.Services;

public sealed class DailyMemberReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyMemberReminderService> _logger;

    public DailyMemberReminderService(IServiceScopeFactory scopeFactory, ILogger<DailyMemberReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A short grace period handles application startup immediately after
        // midnight without postponing that day's reminders for 24 hours.
        if (DateTime.Now.TimeOfDay >= TimeSpan.FromMinutes(5))
        {
            await DelayUntilNextMidnightAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendTodayRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Daily member reminder processing failed.");
            }

            await DelayUntilNextMidnightAsync(stoppingToken);
        }
    }

    private async Task SendTodayRemindersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<DailyMemberReminderSender>();
        var today = DateTime.Today;
        var ids = await context.Clients.AsNoTracking()
            .Where(client => client.IsActive && client.IsActiveMember)
            .Select(client => client.UserId)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            await sender.SendAsync(id, today, cancellationToken);
        }
    }

    private static Task DelayUntilNextMidnightAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var nextMidnight = now.Date.AddDays(1);
        return Task.Delay(nextMidnight - now, cancellationToken);
    }
}
