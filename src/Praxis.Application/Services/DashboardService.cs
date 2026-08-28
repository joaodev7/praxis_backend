using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Enums;

namespace Praxis.Application.Services;

public class DashboardService
{
    private readonly IApplicationDbContext _context;

    public DashboardService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardMetricsDto> GetMetricsAsync()
    {
        var totalClients = await _context.ClientCompanies.CountAsync(c => !c.IsDeleted && c.Status == CommonStatus.Active);
        var totalUnits = await _context.Units.CountAsync(u => !u.IsDeleted && u.Status == CommonStatus.Active);
        var totalNutritionists = await _context.Nutritionists.CountAsync(n => !n.IsDeleted && n.Status == CommonStatus.Active);
        var activeArts = await _context.ARTs.CountAsync(a => !a.IsDeleted && a.Status == ArtStatus.Active);

        var firstDayOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var visitsThisMonth = await _context.Visits.CountAsync(v => !v.IsDeleted && v.ScheduledAt >= firstDayOfMonth);

        var openNCs = await _context.NonConformities.CountAsync(nc => !nc.IsDeleted && nc.Status != NonConformityStatus.Resolvida && nc.Status != NonConformityStatus.Cancelada);
        var lateNCs = await _context.NonConformities.CountAsync(nc => !nc.IsDeleted && nc.Status != NonConformityStatus.Resolvida && nc.Status != NonConformityStatus.Cancelada && nc.DueDate.HasValue && nc.DueDate.Value < DateTime.UtcNow);

        // RB09 Compliance Rate calculation across finished visits
        var allFinishedVisitItems = await _context.VisitItems
            .Include(vi => vi.Visit)
            .Where(vi => vi.Visit != null && !vi.Visit.IsDeleted && vi.Visit.Status == VisitStatus.Finished)
            .ToListAsync();

        var totalConforme = allFinishedVisitItems.Count(i => i.Result == EvaluationResult.Conforme);
        var totalNaoConforme = allFinishedVisitItems.Count(i => i.Result == EvaluationResult.NaoConforme);
        var totalEvaluated = totalConforme + totalNaoConforme;
        double averageComplianceRate = totalEvaluated > 0 ? Math.Round((double)totalConforme / totalEvaluated * 100, 1) : 100.0;

        // Recent visits
        var recentVisits = await _context.Visits
            .Include(v => v.Unit)
                .ThenInclude(u => u!.ClientCompany)
            .Include(v => v.Nutritionist)
                .ThenInclude(n => n!.User)
            .Include(v => v.Items)
            .Where(v => !v.IsDeleted)
            .OrderByDescending(v => v.ScheduledAt)
            .Take(5)
            .ToListAsync();

        var recentVisitDtos = recentVisits.Select(v =>
        {
            var conf = v.Items.Count(i => i.Result == EvaluationResult.Conforme);
            var nonConf = v.Items.Count(i => i.Result == EvaluationResult.NaoConforme);
            var eval = conf + nonConf;
            double? comp = eval > 0 ? Math.Round((double)conf / eval * 100, 1) : null;
            return new RecentVisitDto(v.Id, v.Unit?.ClientCompany?.TradeName ?? string.Empty, v.Unit?.Name ?? string.Empty, v.Nutritionist?.User?.Name ?? string.Empty, v.ScheduledAt, v.Status, comp);
        }).ToList();

        // Critical units (units with most open NCs)
        var criticalUnitsList = await _context.Units
            .Include(u => u.ClientCompany)
            .Include(u => u.Visits)
                .ThenInclude(v => v.NonConformities.Where(nc => !nc.IsDeleted && nc.Status != NonConformityStatus.Resolvida && nc.Status != NonConformityStatus.Cancelada))
            .Where(u => !u.IsDeleted)
            .ToListAsync();

        var criticalUnitDtos = criticalUnitsList
            .Select(u => new CriticalUnitDto(
                u.Id,
                u.Name,
                u.ClientCompany?.TradeName ?? string.Empty,
                u.Visits.SelectMany(v => v.NonConformities).Count(),
                null
            ))
            .Where(u => u.OpenNonConformitiesCount > 0)
            .OrderByDescending(u => u.OpenNonConformitiesCount)
            .Take(5)
            .ToList();

        // Expiring ARTs (in next 30 days)
        var thirtyDaysFromNow = DateTime.UtcNow.AddDays(30);
        var expiringArtsList = await _context.ARTs
            .Include(a => a.Unit)
            .Include(a => a.Nutritionist)
                .ThenInclude(n => n!.User)
            .Where(a => !a.IsDeleted && a.Status == ArtStatus.Active && a.EndDate.HasValue && a.EndDate.Value <= thirtyDaysFromNow)
            .OrderBy(a => a.EndDate)
            .Take(5)
            .ToListAsync();

        var expiringArtDtos = expiringArtsList.Select(a => new ExpiringArtDto(
            a.Id,
            a.Number,
            a.Unit?.Name ?? string.Empty,
            a.Nutritionist?.User?.Name ?? string.Empty,
            a.EndDate,
            a.EndDate.HasValue ? (int)(a.EndDate.Value - DateTime.UtcNow).TotalDays : 0
        )).ToList();

        return new DashboardMetricsDto(
            totalClients,
            totalUnits,
            totalNutritionists,
            activeArts,
            visitsThisMonth,
            openNCs,
            lateNCs,
            averageComplianceRate,
            recentVisitDtos,
            criticalUnitDtos,
            expiringArtDtos
        );
    }
}
