using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class Nutritionist : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Crn { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public CommonStatus Status { get; set; } = CommonStatus.Active;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<ART> ARTs { get; set; } = new List<ART>();
    public ICollection<Visit> Visits { get; set; } = new List<Visit>();
    public ICollection<NutritionistUnitAssignment> UnitAssignments { get; set; } = new List<NutritionistUnitAssignment>();
}
