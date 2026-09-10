using Praxis.Application.DTOs;

namespace Praxis.Application.Interfaces;

public interface IValidityRuleService
{
    Task<List<ValidityRuleDto>> GetAllAsync(Guid? unitId = null, string? category = null);
    Task<ValidityRuleDto> GetByIdAsync(Guid id);
    Task<ValidityRuleDto> CreateAsync(CreateValidityRuleRequest request);
    Task<ValidityRuleDto> UpdateAsync(Guid id, UpdateValidityRuleRequest request);
    Task DeleteAsync(Guid id);
    Task<SimulateValidityResponse> SimulateValidityAsync(SimulateValidityRequest request);
}
