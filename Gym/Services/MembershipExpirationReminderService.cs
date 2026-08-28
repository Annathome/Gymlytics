using Gym.Data;
using Gym.Models;
using Microsoft.EntityFrameworkCore;

namespace Gym.Services;

public sealed class MembershipExpirationReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MembershipExpirationReminderService> _logger;

    public MembershipExpirationReminderService(
        IServiceScopeFactory scopeFactory,
        ILogger<MembershipExpirationReminderService> logger)
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
                await SendRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Membership expiration reminder processing failed.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<GmailValidationService>();
        var today = DateTime.Today;
        var limit = today.AddDays(8);

        // Membership access ends only when the paid plan reaches its end date.
        // Keep the user record, but remove gym access after expiration.
        await context.Clients
            .Where(client => client.IsActiveMember &&
                client.MembershipEndDate.HasValue &&
                client.MembershipEndDate.Value < today &&
                client.Status != MemberStatus.Canceled &&
                client.Status != MemberStatus.Terminated)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(client => client.IsActiveMember, false)
                .SetProperty(client => client.Status, MemberStatus.Inactive),
                cancellationToken);

        var expiringMembers = await context.Clients
            .AsNoTracking()
            .Where(client => client.IsActive && client.IsActiveMember &&
                client.MembershipEndDate.HasValue &&
                client.MembershipEndDate.Value >= today &&
                client.MembershipEndDate.Value < limit)
            .Select(client => new
            {
                client.UserId,
                client.Email,
                FullName = (client.FirstName + " " + client.LastName).Trim(),
                ExpirationDate = client.MembershipEndDate!.Value
            })
            .ToListAsync(cancellationToken);

        foreach (var member in expiringMembers)
        {
            var deliveryKey = $"MembershipExpiration:{member.UserId}:{member.ExpirationDate:yyyyMMdd}";
            var alreadySent = await context.FrontDeskEmailNotifications
                .AsNoTracking()
                .AnyAsync(item => item.DeliveryKey == deliveryKey && item.Status == "Sent", cancellationToken);
            if (alreadySent) continue;

            var (sent, error) = string.IsNullOrWhiteSpace(member.Email)
                ? (false, "The member has no email address.")
                : await emailService.SendMembershipExpirationReminderAsync(
                    member.Email,
                    member.FullName,
                    member.ExpirationDate);

            var status = sent ? "Sent" : "Failed";
            context.FrontDeskEmailNotifications.Add(new FrontDeskEmailNotification
            {
                StaffId = 0,
                Title = $"Email {status}: Membership expiration reminder",
                EmailType = "Membership expiration reminder",
                Status = status,
                DeliveryKey = deliveryKey,
                Message = sent
                    ? $"An expiration reminder was automatically sent to {member.FullName} ({member.Email}) for the membership ending {member.ExpirationDate:MMMM d, yyyy}."
                    : $"The automatic expiration reminder for {member.FullName} ({member.Email}) was not sent. {error}",
                CreatedDate = DateTime.Now
            });
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
