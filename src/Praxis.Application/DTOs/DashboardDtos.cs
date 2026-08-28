using Praxis.Domain.Enums;

namespace Praxis.Application.DTOs;

public record DashboardMetricsDto(
    int TotalClients,
    int TotalUnits,
    int TotalNutritionists,
    int ActiveArts,
    int VisitsThisMonth,
    int OpenNonConformities,
    int LateNonConformities,
    double AverageComplianceRate,
    List<RecentVisitDto> RecentVisits,
    List<CriticalUnitDto> CriticalUnits,
    List<ExpiringArtDto> ExpiringArts
);

public record RecentVisitDto(
    Guid Id,
    string ClientName,
    string UnitName,
    string NutritionistName,
    DateTime Date,
    VisitStatus Status,
    double? ComplianceRate
);

public record CriticalUnitDto(
    Guid UnitId,
    string UnitName,
    string ClientName,
    int OpenNonConformitiesCount,
    double? LastComplianceRate
);

public record ExpiringArtDto(
    Guid Id,
    string Number,
    string UnitName,
    string NutritionistName,
    DateTime? EndDate,
    int DaysRemaining
);
