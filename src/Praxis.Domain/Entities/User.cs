using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class User : BaseEntity, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Nutritionist;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Nutritionist? NutritionistProfile { get; set; }
}
