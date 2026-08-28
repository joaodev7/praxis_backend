using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Praxis.Infrastructure.Billing.BackgroundJobs;
using Praxis.Infrastructure.Data;
using Xunit;

namespace Praxis.Billing.Tests;

public class BillingBackgroundServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly BillingBackgroundService _sut;
    private readonly Guid _tenantId;
    private readonly IDisposable _connection;

    public BillingBackgroundServiceTests()
    {
        _tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(_tenantId);
        _context = context;
        _connection = connection;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite((Microsoft.Data.Sqlite.SqliteConnection)connection)
            .Options;

        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => new ApplicationDbContext(options, currentUserMock.Object));
        _serviceProvider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<BillingBackgroundService>>();
        _sut = new BillingBackgroundService(_serviceProvider, loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ProcessBillingChecks_ShouldExpireEndedTrials()
    {
        // Arrange
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.Status = SubscriptionStatus.Trial;
        sub.TrialEndsAt = DateTime.UtcNow.AddDays(-1); // Expired yesterday
        await _context.SaveChangesAsync();

        // Act - call private method via reflection or test helper
        var method = typeof(BillingBackgroundService)
            .GetMethod("ProcessBillingChecksAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(_sut, new object[] { CancellationToken.None })!;

        // Assert
        await _context.Entry(sub).ReloadAsync();
        sub.Status.Should().Be(SubscriptionStatus.Expired);
    }

    [Fact]
    public async Task ProcessBillingChecks_ShouldSuspendPastDueAfterGracePeriod()
    {
        // Arrange
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.Status = SubscriptionStatus.PastDue;
        sub.GracePeriodEndsAt = DateTime.UtcNow.AddDays(-1); // Grace period ended yesterday
        await _context.SaveChangesAsync();

        // Act
        var method = typeof(BillingBackgroundService)
            .GetMethod("ProcessBillingChecksAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(_sut, new object[] { CancellationToken.None })!;

        // Assert
        await _context.Entry(sub).ReloadAsync();
        sub.Status.Should().Be(SubscriptionStatus.Suspended);
    }

    [Fact]
    public async Task ProcessBillingChecks_ShouldExpireCancelledSubscriptionAfterPeriodEnds()
    {
        // Arrange
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.Status = SubscriptionStatus.Cancelled;
        sub.EndsAtPeriodEnd = true;
        sub.CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1); // Paid period ended yesterday
        await _context.SaveChangesAsync();

        // Act
        var method = typeof(BillingBackgroundService)
            .GetMethod("ProcessBillingChecksAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(_sut, new object[] { CancellationToken.None })!;

        // Assert
        await _context.Entry(sub).ReloadAsync();
        sub.Status.Should().Be(SubscriptionStatus.Expired);
    }
}
