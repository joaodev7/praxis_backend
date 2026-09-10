using Praxis.Domain.Common;

namespace Praxis.Domain.Entities;

public class LabelTemplate : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Name { get; set; } = string.Empty;
    public string TemplateType { get; set; } = "Thermal50x30"; // "Thermal50x30", "Thermal80x50", "SheetA4"
    public decimal WidthMm { get; set; } = 50;
    public decimal HeightMm { get; set; } = 30;
    public bool IncludeQrCode { get; set; } = true;
    public bool IncludeLogo { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
}
