using Microsoft.EntityFrameworkCore;
using Praxis.Application.Interfaces;
using Praxis.Domain.Common;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using System.Linq.Expressions;

namespace Praxis.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUser;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Nutritionist> Nutritionists => Set<Nutritionist>();
    public DbSet<NutritionistUnitAssignment> NutritionistUnitAssignments => Set<NutritionistUnitAssignment>();
    public DbSet<ClientCompany> ClientCompanies => Set<ClientCompany>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<ART> ARTs => Set<ART>();
    public DbSet<Checklist> Checklists => Set<Checklist>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<VisitItem> VisitItems => Set<VisitItem>();
    public DbSet<NonConformity> NonConformities => Set<NonConformity>();
    public DbSet<ActionItem> ActionItems => Set<ActionItem>();
    public DbSet<Evidence> Evidences => Set<Evidence>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure indexes and relationships
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(t => t.Cnpj).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasOne(u => u.Tenant)
                  .WithMany(t => t.Users)
                  .HasForeignKey(u => u.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Nutritionist>(entity =>
        {
            entity.HasOne(n => n.User)
                  .WithOne(u => u.NutritionistProfile)
                  .HasForeignKey<Nutritionist>(n => n.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NutritionistUnitAssignment>(entity =>
        {
            entity.HasKey(nua => new { nua.NutritionistId, nua.UnitId });
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasOne(u => u.ClientCompany)
                  .WithMany(c => c.Units)
                  .HasForeignKey(u => u.ClientCompanyId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ART>(entity =>
        {
            entity.HasOne(a => a.Unit)
                  .WithMany(u => u.ARTs)
                  .HasForeignKey(a => a.UnitId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Nutritionist)
                  .WithMany(n => n.ARTs)
                  .HasForeignKey(a => a.NutritionistId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Visit>(entity =>
        {
            entity.HasOne(v => v.Unit)
                  .WithMany(u => u.Visits)
                  .HasForeignKey(v => v.UnitId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Nutritionist)
                  .WithMany(n => n.Visits)
                  .HasForeignKey(v => v.NutritionistId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NonConformity>(entity =>
        {
            entity.HasOne(nc => nc.Visit)
                  .WithMany(v => v.NonConformities)
                  .HasForeignKey(nc => nc.VisitId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(nc => nc.VisitItem)
                  .WithOne(vi => vi.NonConformity)
                  .HasForeignKey<NonConformity>(nc => nc.VisitItemId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Apply Multi-tenancy & Soft Delete Query Filters to all relevant entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            var isTenantEntity = typeof(ITenantEntity).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            if (isTenantEntity || isSoftDeletable)
            {
                var parameter = Expression.Parameter(clrType, "e");
                Expression? filter = null;

                if (isTenantEntity)
                {
                    // e => _currentUser.Role == UserRole.PraxisAdmin || !_currentUser.TenantId.HasValue || ((ITenantEntity)e).TenantId == _currentUser.TenantId.Value
                    var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                    var currentTenantProp = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
                    var tenantEquals = Expression.Equal(tenantIdProp, currentTenantProp);

                    var hasTenant = Expression.Property(Expression.Constant(this), nameof(HasTenantFilter));
                    filter = Expression.OrElse(Expression.Not(hasTenant), tenantEquals);
                }

                if (isSoftDeletable)
                {
                    var isDeletedProp = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                    var notDeleted = Expression.Equal(isDeletedProp, Expression.Constant(false));

                    filter = filter == null ? notDeleted : Expression.AndAlso(filter, notDeleted);
                }

                if (filter != null)
                {
                    var lambda = Expression.Lambda(filter, parameter);
                    modelBuilder.Entity(clrType).HasQueryFilter(lambda);
                }
            }
        }
    }

    public Guid CurrentTenantId => _currentUser.TenantId ?? Guid.Empty;
    public bool HasTenantFilter => _currentUser.Role != UserRole.PraxisAdmin && _currentUser.TenantId.HasValue;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries().ToList();
        var auditList = new List<AuditLog>();

        foreach (var entry in entries)
        {
            if (entry.Entity is BaseEntity baseEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    if (baseEntity.CreatedAt == default)
                        baseEntity.CreatedAt = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    baseEntity.UpdatedAt = DateTime.UtcNow;
                }
            }

            if (entry.Entity is ITenantEntity tenantEntity && entry.State == EntityState.Added)
            {
                if (tenantEntity.TenantId == Guid.Empty && _currentUser.TenantId.HasValue)
                {
                    tenantEntity.TenantId = _currentUser.TenantId.Value;
                }
            }

            // Create audit log for changes (excluding AuditLog itself to prevent loop)
            if (entry.Entity is not AuditLog && 
                (entry.State == EntityState.Added || entry.State == EntityState.Modified || entry.State == EntityState.Deleted))
            {
                var entityType = entry.Entity.GetType().Name;
                var action = entry.State switch
                {
                    EntityState.Added => "CREATE",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted => "DELETE",
                    _ => entry.State.ToString()
                };

                var entityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? string.Empty;
                var tenantId = _currentUser.TenantId;
                if (entry.Entity is ITenantEntity te && te.TenantId != Guid.Empty)
                {
                    tenantId = te.TenantId;
                }

                auditList.Add(new AuditLog
                {
                    TenantId = tenantId,
                    UserId = _currentUser.UserId,
                    Action = action,
                    Entity = entityType,
                    EntityId = entityId,
                    Metadata = $"Ação {action} em {entityType} pelo usuário {_currentUser.UserEmail ?? _currentUser.UserId?.ToString() ?? "Sistema"}"
                });
            }
        }

        if (auditList.Count > 0)
        {
            AuditLogs.AddRange(auditList);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
