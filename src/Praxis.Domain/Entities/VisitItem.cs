using Praxis.Domain.Common;
using Praxis.Domain.Enums;

namespace Praxis.Domain.Entities;

public class VisitItem : BaseEntity
{
    public Guid VisitId { get; set; }
    public Visit? Visit { get; set; }

    public Guid ChecklistItemId { get; set; }
    public ChecklistItem? ChecklistItem { get; set; }

    public EvaluationResult Result { get; set; } = EvaluationResult.Conforme;
    public string? Observation { get; set; }

    public NonConformity? NonConformity { get; set; }
}
