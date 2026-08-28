using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Praxis.Application.Services;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Praxis.Infrastructure.Data;
using Xunit;

namespace Praxis.Billing.Tests;

public class EntitlementServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly EntitlementService _sut;
    private readonly Guid _tenantId;
    private readonly IDisposable _connection;

    public EntitlementServiceTests()
    {
        _tenantId = Guid.NewGuid();
        var (context, _, connection) = TestDbContextFactory.CreateInMemoryDbContext(_tenantId);
        _context = context;
        _connection = connection;
        _sut = new EntitlementService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task HasFeatureAsync_ShouldReturnTrue_WhenFeatureIsEnabledInPlan()
    {
        var result = await _sut.HasFeatureAsync(_tenantId, "advanced_analytics");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasFeatureAsync_ShouldReturnFalse_WhenFeatureIsNotInPlan()
    {
        // Switch to essential plan
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        var essential = await _context.Plans.FirstAsync(p => p.Code == "essential");
        sub.PlanId = essential.Id;
        await _context.SaveChangesAsync();

        var result = await _sut.HasFeatureAsync(_tenantId, "advanced_analytics");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasFeatureAsync_ShouldRespectSubscriptionOverrides()
    {
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        var essential = await _context.Plans.FirstAsync(p => p.Code == "essential");
        sub.PlanId = essential.Id;
        _context.SubscriptionFeatureOverrides.Add(new SubscriptionFeatureOverride
        {
            SubscriptionId = sub.Id,
            FeatureCode = "advanced_analytics",
            IsEnabled = true
        });
        await _context.SaveChangesAsync();

        var result = await _sut.HasFeatureAsync(_tenantId, "advanced_analytics");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateLimitAsync_ShouldThrow_WhenMaxNutritionistsLimitExceeded()
    {
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        var essential = await _context.Plans.FirstAsync(p => p.Code == "essential");
        sub.PlanId = essential.Id; // limit: 3
        await _context.SaveChangesAsync();

        // Add 3 nutritionists with users
        for (int i = 0; i < 3; i++)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Name = $"Nutri {i}",
                Email = $"nutri_{i}@test.com",
                PasswordHash = "hash",
                Role = UserRole.Nutritionist,
                Status = UserStatus.Active
            };
            _context.Users.Add(user);

            _context.Nutritionists.Add(new Nutritionist
            {
                TenantId = _tenantId,
                UserId = user.Id,
                Crn = $"CRN-{i}",
                Status = CommonStatus.Active
            });
        }
        await _context.SaveChangesAsync();

        // Attempting to add 4th should throw InvalidOperationException
        var act = () => _sut.ValidateLimitAsync(_tenantId, "max_nutritionists", 1);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Limite de nutricionistas atingido*");
    }

    [Fact]
    public async Task ValidateLimitAsync_ShouldSucceed_WhenWithinLimit()
    {
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        var essential = await _context.Plans.FirstAsync(p => p.Code == "essential");
        sub.PlanId = essential.Id; // limit: 3
        await _context.SaveChangesAsync();

        // Add 2 nutritionists with users
        for (int i = 0; i < 2; i++)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Name = $"Nutri {i}",
                Email = $"nutri_{i}@test.com",
                PasswordHash = "hash",
                Role = UserRole.Nutritionist,
                Status = UserStatus.Active
            };
            _context.Users.Add(user);

            _context.Nutritionists.Add(new Nutritionist
            {
                TenantId = _tenantId,
                UserId = user.Id,
                Crn = $"CRN-{i}",
                Status = CommonStatus.Active
            });
        }
        await _context.SaveChangesAsync();

        // Adding 3rd should succeed without exception
        var act = () => _sut.ValidateLimitAsync(_tenantId, "max_nutritionists", 1);
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(SubscriptionStatus.Trial, 5, true)]   // Trial active with 5 days remaining -> true
    [InlineData(SubscriptionStatus.Trial, -1, false)] // Trial expired 1 day ago -> false
    [InlineData(SubscriptionStatus.Active, 0, true)]   // Active subscription -> true
    [InlineData(SubscriptionStatus.Suspended, 0, false)] // Suspended -> false
    [InlineData(SubscriptionStatus.Expired, 0, false)]   // Expired -> false
    public async Task HasActiveAccessAsync_ShouldCorrectlyDetermineAccess(SubscriptionStatus status, int daysOffset, bool expectedAccess)
    {
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.Status = status;
        if (status == SubscriptionStatus.Trial)
        {
            sub.TrialEndsAt = DateTime.UtcNow.AddDays(daysOffset);
        }
        await _context.SaveChangesAsync();

        var result = await _sut.HasActiveAccessAsync(_tenantId);
        result.Should().Be(expectedAccess);
    }

    [Fact]
    public async Task HasActiveAccessAsync_PastDueWithinGracePeriod_ShouldReturnTrue()
    {
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.Status = SubscriptionStatus.PastDue;
        sub.GracePeriodEndsAt = DateTime.UtcNow.AddDays(4); // 4 days remaining in grace period
        await _context.SaveChangesAsync();

        var result = await _sut.HasActiveAccessAsync(_tenantId);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveAccessAsync_PastDueAfterGracePeriod_ShouldReturnFalse()
    {
        var sub = await _context.Subscriptions.FirstAsync(s => s.TenantId == _tenantId);
        sub.Status = SubscriptionStatus.PastDue;
        sub.GracePeriodEndsAt = DateTime.UtcNow.AddDays(-2); // grace period ended 2 days ago
        await _context.SaveChangesAsync();

        var result = await _sut.HasActiveAccessAsync(_tenantId);
        result.Should().BeFalse();
    }
}
