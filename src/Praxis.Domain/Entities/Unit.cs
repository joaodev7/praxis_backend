using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class Unit : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid ClientCompanyId { get; set; }
    public ClientCompany? ClientCompany { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string ResponsibleName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public CommonStatus Status { get; set; } = CommonStatus.Active;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<ART> ARTs { get; set; } = new List<ART>();
    public ICollection<Visit> Visits { get; set; } = new List<Visit>();
    public ICollection<NutritionistUnitAssignment> NutritionistAssignments { get; set; } = new List<NutritionistUnitAssignment>();
}
