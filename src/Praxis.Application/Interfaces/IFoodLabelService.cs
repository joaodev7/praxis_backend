using Praxis.Application.DTOs;

namespace Praxis.Application.Interfaces;

public interface IFoodLabelService
{
    Task<List<FoodLabelListDto>> GetAllAsync(FoodLabelFilterParams filter);
    Task<FoodLabelDto> GetByIdAsync(Guid id);
    Task<FoodLabelDto> CreateAsync(CreateFoodLabelRequest request);
    Task<FoodLabelDto> CancelAsync(Guid id, CancelFoodLabelRequest request);
    Task<FoodLabelDto> DiscardAsync(Guid id, DiscardFoodLabelRequest request);
    Task<FoodLabelDto> ReprintAsync(Guid id, ReprintFoodLabelRequest request);
    Task<FoodLabelDashboardDto> GetDashboardStatsAsync(Guid? unitId = null);
    Task<FoodLabelPublicDto> GetPublicByTokenAsync(string publicToken);
    Task<List<LabelTemplateDto>> GetTemplatesAsync();
}
