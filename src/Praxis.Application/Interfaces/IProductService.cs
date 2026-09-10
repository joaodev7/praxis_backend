using Praxis.Application.DTOs;

namespace Praxis.Application.Interfaces;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync(Guid? unitId = null, string? category = null);
    Task<ProductDto> GetByIdAsync(Guid id);
    Task<ProductDto> CreateAsync(CreateProductRequest request);
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request);
    Task DeleteAsync(Guid id);
    Task<List<ProductBatchDto>> GetBatchesByProductIdAsync(Guid productId);
    Task<ProductBatchDto> CreateBatchAsync(Guid productId, CreateProductBatchRequest request);
}
