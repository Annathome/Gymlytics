using Gym.Data;
using Gym.Models;
using Microsoft.EntityFrameworkCore;

namespace Gym.Services;

public sealed class MemberInactivityService : BackgroundService
{
    private static readonly TimeSpan InactivityPeriod = TimeSpan.FromDays(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemberInactivityService> _logger;

    public MemberInactivityService(IServiceScopeFactory scopeFactory, ILogger<MemberInactivityService> logger)
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
                await MarkInactiveMembersAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Member inactivity processing failed.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task MarkInactiveMembersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        var cutoff = DateTime.Now.Subtract(InactivityPeriod);

        var activeMembers = await context.Clients
            .Where(client => client.IsActive && client.IsActiveMember &&
                client.Status != MemberStatus.Inactive &&
                client.Status != MemberStatus.Canceled &&
                client.Status != MemberStatus.Terminated)
            .ToListAsync(cancellationToken);

        if (activeMembers.Count == 0) return;

        var memberIds = activeMembers.Select(client => client.UserId).ToList();
        var lastAttendanceByMember = await context.MemberAttendanceLogs
            .Where(log => memberIds.Contains(log.ClientId))
            .GroupBy(log => log.ClientId)
            .Select(group => new { ClientId = group.Key, LastCheckIn = group.Max(log => log.CheckInAt) })
            .ToDictionaryAsync(item => item.ClientId, item => item.LastCheckIn, cancellationToken);

        var newlyInactive = 0;
        foreach (var member in activeMembers)
        {
            var lastActivity = lastAttendanceByMember.TryGetValue(member.UserId, out var lastCheckIn)
                ? lastCheckIn
                : member.CreatedDate;

            if (lastActivity > cutoff) continue;

            member.Status = MemberStatus.Inactive;
            member.IsActiveMember = false;
            member.IsActive = false;
            newlyInactive++;
        }

        if (newlyInactive == 0) return;

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Marked {Count} members inactive after 15 days without attendance.", newlyInactive);
    }
}
