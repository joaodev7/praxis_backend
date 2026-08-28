using Microsoft.EntityFrameworkCore;
using Praxis.Domain.Entities;

namespace Praxis.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<Nutritionist> Nutritionists { get; }
    DbSet<NutritionistUnitAssignment> NutritionistUnitAssignments { get; }
    DbSet<ClientCompany> ClientCompanies { get; }
    DbSet<Unit> Units { get; }
    DbSet<ART> ARTs { get; }
    DbSet<Checklist> Checklists { get; }
    DbSet<ChecklistItem> ChecklistItems { get; }
    DbSet<Visit> Visits { get; }
    DbSet<VisitItem> VisitItems { get; }
    DbSet<NonConformity> NonConformities { get; }
    DbSet<ActionItem> ActionItems { get; }
    DbSet<Evidence> Evidences { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
