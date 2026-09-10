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
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionFeatureOverride> SubscriptionFeatureOverrides => Set<SubscriptionFeatureOverride>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<StoredFile> Files => Set<StoredFile>();
    public DbSet<ActionPlanEvidence> ActionPlanEvidences => Set<ActionPlanEvidence>();

    // Etiquetagem e Gestão de Validade
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<ValidityRule> ValidityRules => Set<ValidityRule>();
    public DbSet<FoodLabel> FoodLabels => Set<FoodLabel>();
    public DbSet<LabelTemplate> LabelTemplates => Set<LabelTemplate>();
    public DbSet<LabelPrint> LabelPrints => Set<LabelPrint>();
    public DbSet<FoodLabelAudit> FoodLabelAudits => Set<FoodLabelAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure indexes and relationships
        modelBuilder.Entity<StoredFile>(entity =>
        {
            entity.HasIndex(f => f.ObjectKey);
            entity.HasIndex(f => f.TenantId);
            entity.HasIndex(f => f.ClientId);
            entity.HasIndex(f => f.Status);

            entity.HasOne(f => f.Tenant)
                  .WithMany()
                  .HasForeignKey(f => f.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.UploadedByUser)
                  .WithMany()
                  .HasForeignKey(f => f.UploadedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(f => f.Client)
                  .WithMany()
                  .HasForeignKey(f => f.ClientId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.HasIndex(p => p.Code).IsUnique();
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasOne(s => s.Tenant)
                  .WithMany(t => t.Subscriptions)
                  .HasForeignKey(s => s.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Plan)
                  .WithMany(p => p.Subscriptions)
                  .HasForeignKey(s => s.PlanId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasOne(p => p.Subscription)
                  .WithMany(s => s.Payments)
                  .HasForeignKey(p => p.SubscriptionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Tenant)
                  .WithMany(t => t.Payments)
                  .HasForeignKey(p => p.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => p.ProviderPaymentId);
        });

        modelBuilder.Entity<PaymentWebhookEvent>(entity =>
        {
            entity.HasIndex(w => new { w.Provider, w.ProviderEventId }).IsUnique();
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(t => t.Cnpj).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Name).HasMaxLength(150).IsRequired();
            entity.Property(u => u.ProfilePhotoKey).HasMaxLength(500);
            entity.Property(u => u.ProfilePhotoUrl).HasMaxLength(1000);
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

        modelBuilder.Entity<ActionItem>(entity =>
        {
            entity.HasOne(a => a.NonConformity)
                  .WithMany(nc => nc.Actions)
                  .HasForeignKey(a => a.NonConformityId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.ResponsibleUser)
                  .WithMany()
                  .HasForeignKey(a => a.ResponsibleUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.ValidatedByUser)
                  .WithMany()
                  .HasForeignKey(a => a.ValidatedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Tenant)
                  .WithMany()
                  .HasForeignKey(a => a.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(a => a.NonConformityId);
            entity.HasIndex(a => a.TenantId);
            entity.HasIndex(a => a.ResponsibleUserId);
            entity.HasIndex(a => a.Status);
            entity.HasIndex(a => a.Priority);
            entity.HasIndex(a => a.DueDate);
        });

        modelBuilder.Entity<ActionPlanEvidence>(entity =>
        {
            entity.HasOne(e => e.ActionPlan)
                  .WithMany(a => a.Evidences)
                  .HasForeignKey(e => e.ActionPlanId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Tenant)
                  .WithMany()
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.UploadedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.UploadedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ActionPlanId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.ObjectKey);
        });

        // Etiquetagem e Gestão de Validade Configurations
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasOne(p => p.Tenant)
                  .WithMany()
                  .HasForeignKey(p => p.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Unit)
                  .WithMany()
                  .HasForeignKey(p => p.UnitId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => new { p.TenantId, p.UnitId });
            entity.HasIndex(p => new { p.TenantId, p.Category });
        });

        modelBuilder.Entity<ProductBatch>(entity =>
        {
            entity.HasOne(b => b.Tenant)
                  .WithMany()
                  .HasForeignKey(b => b.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Product)
                  .WithMany(p => p.Batches)
                  .HasForeignKey(b => b.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(b => new { b.TenantId, b.ProductId });
            entity.HasIndex(b => new { b.TenantId, b.BatchCode });
        });

        modelBuilder.Entity<ValidityRule>(entity =>
        {
            entity.HasOne(r => r.Tenant)
                  .WithMany()
                  .HasForeignKey(r => r.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Unit)
                  .WithMany()
                  .HasForeignKey(r => r.UnitId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(r => r.Product)
                  .WithMany()
                  .HasForeignKey(r => r.ProductId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(r => new { r.TenantId, r.IsActive, r.UnitId, r.ProductId });
        });

        modelBuilder.Entity<FoodLabel>(entity =>
        {
            entity.HasOne(l => l.Tenant)
                  .WithMany()
                  .HasForeignKey(l => l.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Unit)
                  .WithMany()
                  .HasForeignKey(l => l.UnitId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Product)
                  .WithMany(p => p.FoodLabels)
                  .HasForeignKey(l => l.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.ProductBatch)
                  .WithMany(b => b.FoodLabels)
                  .HasForeignKey(l => l.ProductBatchId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(l => l.ValidityRule)
                  .WithMany(r => r.FoodLabels)
                  .HasForeignKey(l => l.ValidityRuleId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(l => l.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(l => l.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.CancelledByUser)
                  .WithMany()
                  .HasForeignKey(l => l.CancelledByUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(l => l.DiscardedByUser)
                  .WithMany()
                  .HasForeignKey(l => l.DiscardedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(l => new { l.TenantId, l.UnitId });
            entity.HasIndex(l => new { l.TenantId, l.CalculatedExpirationDate });
            entity.HasIndex(l => new { l.TenantId, l.ProductId });
            entity.HasIndex(l => new { l.TenantId, l.InternalBatchCode });
            entity.HasIndex(l => l.PublicToken).IsUnique();
        });

        modelBuilder.Entity<LabelPrint>(entity =>
        {
            entity.HasOne(p => p.Label)
                  .WithMany(l => l.Prints)
                  .HasForeignKey(p => p.LabelId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.PrintedByUser)
                  .WithMany()
                  .HasForeignKey(p => p.PrintedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => new { p.TenantId, p.LabelId });
        });

        modelBuilder.Entity<FoodLabelAudit>(entity =>
        {
            entity.HasOne(a => a.Label)
                  .WithMany(l => l.Audits)
                  .HasForeignKey(a => a.LabelId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => new { a.TenantId, a.LabelId });
        });

        modelBuilder.Entity<LabelTemplate>(entity =>
        {
            entity.HasIndex(t => new { t.TenantId, t.TemplateType });
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
