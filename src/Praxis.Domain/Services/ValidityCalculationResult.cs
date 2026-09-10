using Praxis.Domain.Enums;

namespace Praxis.Domain.Services;

public class ValidityCalculationResult
{
    public DateTime StartAt { get; init; }
    public DateTime ExpirationDate { get; init; }
    public ValiditySource Source { get; init; }
    public Guid? RuleId { get; init; }
    public string? Explanation { get; init; }
    public string? TechnicalBasis { get; init; }
    public string? RegulatoryReference { get; init; }
    public bool IsManual { get; init; }
    public bool RuleMatched { get; init; }
}
