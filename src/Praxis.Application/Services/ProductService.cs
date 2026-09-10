using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;

namespace Praxis.Application.Services;

public class ProductService : IProductService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ProductService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<ProductDto>> GetAllAsync(Guid? unitId = null, string? category = null)
    {
        var query = _context.Products
            .Include(p => p.Unit)
            .Include(p => p.Batches.Where(b => !b.IsDeleted))
            .Where(p => !p.IsDeleted && p.IsActive);

        if (unitId.HasValue)
        {
            // Retorna produtos do catálogo geral (UnitId == null) e específicos desta unidade (UnitId == unitId)
            query = query.Where(p => p.UnitId == null || p.UnitId == unitId.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        var list = await query.OrderBy(p => p.Name).ToListAsync();

        return list.Select(p => new ProductDto(
            p.Id,
            p.TenantId,
            p.UnitId,
            p.Unit?.Name,
            p.Name,
            p.Description,
            p.Category,
            p.IsActive,
            p.CreatedAt,
            p.Batches.Count
        )).ToList();
    }

    public async Task<ProductDto> GetByIdAsync(Guid id)
    {
        var p = await _context.Products
            .Include(p => p.Unit)
            .Include(p => p.Batches.Where(b => !b.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (p == null) throw new KeyNotFoundException("Produto não encontrado.");

        return new ProductDto(
            p.Id,
            p.TenantId,
            p.UnitId,
            p.Unit?.Name,
            p.Name,
            p.Description,
            p.Category,
            p.IsActive,
            p.CreatedAt,
            p.Batches.Count
        );
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("O nome do produto é obrigatório.");

        var product = new Product
        {
            TenantId = _currentUser.TenantId ?? Guid.Empty,
            UnitId = request.UnitId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Category = request.Category?.Trim(),
            IsActive = true
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        string? unitName = null;
        if (product.UnitId.HasValue)
        {
            var u = await _context.Units.FirstOrDefaultAsync(x => x.Id == product.UnitId.Value);
            unitName = u?.Name;
        }

        return new ProductDto(
            product.Id,
            product.TenantId,
            product.UnitId,
            unitName,
            product.Name,
            product.Description,
            product.Category,
            product.IsActive,
            product.CreatedAt,
            0
        );
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request)
    {
        var product = await _context.Products
            .Include(p => p.Unit)
            .Include(p => p.Batches.Where(b => !b.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (product == null) throw new KeyNotFoundException("Produto não encontrado.");

        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Category = request.Category?.Trim();
        product.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return new ProductDto(
            product.Id,
            product.TenantId,
            product.UnitId,
            product.Unit?.Name,
            product.Name,
            product.Description,
            product.Category,
            product.IsActive,
            product.CreatedAt,
            product.Batches.Count
        );
    }

    public async Task DeleteAsync(Guid id)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (product == null) throw new KeyNotFoundException("Produto não encontrado.");

        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        product.IsActive = false;

        await _context.SaveChangesAsync();
    }

    public async Task<List<ProductBatchDto>> GetBatchesByProductIdAsync(Guid productId)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
        if (product == null) throw new KeyNotFoundException("Produto não encontrado.");

        var batches = await _context.ProductBatches
            .Where(b => b.ProductId == productId && !b.IsDeleted)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return batches.Select(b => new ProductBatchDto(
            b.Id,
            b.ProductId,
            product.Name,
            b.BatchCode,
            b.OriginalBatchCode,
            b.ManufacturingDate,
            b.OriginalExpirationDate,
            b.CreatedAt
        )).ToList();
    }

    public async Task<ProductBatchDto> CreateBatchAsync(Guid productId, CreateProductBatchRequest request)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
        if (product == null) throw new KeyNotFoundException("Produto não encontrado.");

        if (string.IsNullOrWhiteSpace(request.BatchCode))
            throw new ArgumentException("O código do lote é obrigatório.");

        var batch = new ProductBatch
        {
            TenantId = _currentUser.TenantId ?? Guid.Empty,
            ProductId = productId,
            BatchCode = request.BatchCode.Trim(),
            OriginalBatchCode = request.OriginalBatchCode?.Trim(),
            ManufacturingDate = request.ManufacturingDate,
            OriginalExpirationDate = request.OriginalExpirationDate
        };

        _context.ProductBatches.Add(batch);
        await _context.SaveChangesAsync();

        return new ProductBatchDto(
            batch.Id,
            batch.ProductId,
            product.Name,
            batch.BatchCode,
            batch.OriginalBatchCode,
            batch.ManufacturingDate,
            batch.OriginalExpirationDate,
            batch.CreatedAt
        );
    }
}
