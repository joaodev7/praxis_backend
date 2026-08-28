using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Praxis.Application.Interfaces;
using Praxis.Domain.Enums;

namespace Praxis.Infrastructure.Billing.BackgroundJobs;

public class BillingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BillingBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public BillingBackgroundService(IServiceProvider serviceProvider, ILogger<BillingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BillingBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBillingChecksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing billing background checks");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessBillingChecksAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var now = DateTime.UtcNow;

        // 1. Expire ended Trials
        var expiredTrials = await context.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.Trial && s.TrialEndsAt != null && s.TrialEndsAt.Value < now)
            .ToListAsync(ct);

        foreach (var sub in expiredTrials)
        {
            _logger.LogInformation("Subscription {SubId} trial expired.", sub.Id);
            sub.Status = SubscriptionStatus.Expired;
            sub.UpdatedAt = now;
        }

        // 2. Suspend past due accounts after grace period
        var pastDueToSuspend = await context.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.PastDue && s.GracePeriodEndsAt != null && s.GracePeriodEndsAt.Value < now)
            .ToListAsync(ct);

        foreach (var sub in pastDueToSuspend)
        {
            _logger.LogInformation("Subscription {SubId} past grace period, suspending access.", sub.Id);
            sub.Status = SubscriptionStatus.Suspended;
            sub.UpdatedAt = now;
        }

        // 3. Process scheduled cancellations after period ends
        var cancelledToFinish = await context.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.Cancelled && s.EndsAtPeriodEnd && s.CurrentPeriodEnd != null && s.CurrentPeriodEnd.Value < now)
            .ToListAsync(ct);

        foreach (var sub in cancelledToFinish)
        {
            _logger.LogInformation("Subscription {SubId} cancelled period finished.", sub.Id);
            sub.Status = SubscriptionStatus.Expired;
            sub.UpdatedAt = now;
        }

        if (expiredTrials.Count > 0 || pastDueToSuspend.Count > 0 || cancelledToFinish.Count > 0)
        {
            await context.SaveChangesAsync(ct);
        }
    }
}
