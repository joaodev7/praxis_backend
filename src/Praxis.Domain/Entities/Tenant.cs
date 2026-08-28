using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class Tenant : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Nutritionist> Nutritionists { get; set; } = new List<Nutritionist>();
    public ICollection<ClientCompany> ClientCompanies { get; set; } = new List<ClientCompany>();
    public ICollection<Checklist> Checklists { get; set; } = new List<Checklist>();
}
