using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Services;

namespace Praxis.Application.Services;

public class ValidityRuleService : IValidityRuleService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidityCalculator _validityCalculator;

    public ValidityRuleService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IValidityCalculator validityCalculator)
    {
        _context = context;
        _currentUser = currentUser;
        _validityCalculator = validityCalculator;
    }

    public async Task<List<ValidityRuleDto>> GetAllAsync(Guid? unitId = null, string? category = null)
    {
        var query = _context.ValidityRules
            .Include(r => r.Unit)
            .Include(r => r.Product)
            .Where(r => !r.IsDeleted);

        if (unitId.HasValue)
        {
            query = query.Where(r => r.UnitId == null || r.UnitId == unitId.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(r => r.ProductCategory == category);
        }

        var rules = await query
            .OrderBy(r => r.ProductCategory)
            .ThenBy(r => r.Name)
            .ToListAsync();

        return rules.Select(MapToDto).ToList();
    }

    public async Task<ValidityRuleDto> GetByIdAsync(Guid id)
    {
        var rule = await _context.ValidityRules
            .Include(r => r.Unit)
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (rule == null) throw new KeyNotFoundException("Regra de validade não encontrada.");

        return MapToDto(rule);
    }

    public async Task<ValidityRuleDto> CreateAsync(CreateValidityRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("O nome da regra é obrigatório.");

        if (request.ValidityValue <= 0)
            throw new ArgumentException("O prazo de validade deve ser maior que zero.");

        var rule = new ValidityRule
        {
            TenantId = _currentUser.TenantId ?? Guid.Empty,
            UnitId = request.UnitId,
            ProductId = request.ProductId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ProductCategory = request.ProductCategory?.Trim(),
            LabelType = request.LabelType,
            OperationType = request.OperationType,
            StorageCondition = request.StorageCondition,
            MaximumTemperature = request.MaximumTemperature,
            ValidityValue = request.ValidityValue,
            ValidityUnit = request.ValidityUnit,
            AllowManualExpiration = request.AllowManualExpiration,
            RequiresTechnicalBasis = request.RequiresTechnicalBasis,
            TechnicalBasis = request.TechnicalBasis?.Trim(),
            RegulatoryReference = request.RegulatoryReference?.Trim(),
            IsActive = true
        };

        _context.ValidityRules.Add(rule);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(rule.Id);
    }

    public async Task<ValidityRuleDto> UpdateAsync(Guid id, UpdateValidityRuleRequest request)
    {
        var rule = await _context.ValidityRules.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        if (rule == null) throw new KeyNotFoundException("Regra de validade não encontrada.");

        if (request.ValidityValue <= 0)
            throw new ArgumentException("O prazo de validade deve ser maior que zero.");

        rule.Name = request.Name.Trim();
        rule.Description = request.Description?.Trim();
        rule.ProductCategory = request.ProductCategory?.Trim();
        rule.LabelType = request.LabelType;
        rule.OperationType = request.OperationType;
        rule.StorageCondition = request.StorageCondition;
        rule.MaximumTemperature = request.MaximumTemperature;
        rule.ValidityValue = request.ValidityValue;
        rule.ValidityUnit = request.ValidityUnit;
        rule.AllowManualExpiration = request.AllowManualExpiration;
        rule.RequiresTechnicalBasis = request.RequiresTechnicalBasis;
        rule.TechnicalBasis = request.TechnicalBasis?.Trim();
        rule.RegulatoryReference = request.RegulatoryReference?.Trim();
        rule.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(rule.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var rule = await _context.ValidityRules.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        if (rule == null) throw new KeyNotFoundException("Regra de validade não encontrada.");

        // Soft delete para não violar integridade de etiquetas históricas
        rule.IsDeleted = true;
        rule.DeletedAt = DateTime.UtcNow;
        rule.IsActive = false;

        await _context.SaveChangesAsync();
    }

    public async Task<SimulateValidityResponse> SimulateValidityAsync(SimulateValidityRequest request)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted);
        if (product == null) throw new KeyNotFoundException("Produto não encontrado.");

        DateTime? originalExpDate = null;
        if (request.ProductBatchId.HasValue)
        {
            var batch = await _context.ProductBatches.FirstOrDefaultAsync(b => b.Id == request.ProductBatchId.Value && !b.IsDeleted);
            originalExpDate = batch?.OriginalExpirationDate;
        }

        var tenantId = _currentUser.TenantId ?? Guid.Empty;
        var activeRules = await _context.ValidityRules
            .Where(r => r.IsActive && !r.IsDeleted)
            .ToListAsync();

        var matchedRule = _validityCalculator.ResolveRule(
            tenantId,
            request.UnitId,
            product.Id,
            product.Category,
            request.LabelType,
            request.OperationType,
            request.StorageCondition,
            request.StorageTemperature,
            activeRules
        );

        DateTime startAt = request.StartAt ?? DateTime.UtcNow;
        var result = _validityCalculator.Calculate(startAt, matchedRule, originalExpirationDate: originalExpDate);

        return new SimulateValidityResponse(
            RuleMatched: result.RuleMatched,
            RuleId: matchedRule?.Id,
            RuleName: matchedRule?.Name,
            StartAt: result.StartAt,
            ExpirationDate: result.ExpirationDate,
            Source: result.Source,
            Explanation: result.Explanation,
            TechnicalBasis: result.TechnicalBasis,
            RegulatoryReference: result.RegulatoryReference,
            RequiresManualInput: !result.RuleMatched,
            MaxAllowedDate: originalExpDate
        );
    }

    private static ValidityRuleDto MapToDto(ValidityRule r) => new(
        r.Id,
        r.TenantId,
        r.UnitId,
        r.Unit?.Name,
        r.ProductId,
        r.Product?.Name,
        r.Name,
        r.Description,
        r.ProductCategory,
        r.LabelType,
        r.OperationType,
        r.StorageCondition,
        r.MaximumTemperature,
        r.ValidityValue,
        r.ValidityUnit,
        r.AllowManualExpiration,
        r.RequiresTechnicalBasis,
        r.TechnicalBasis,
        r.RegulatoryReference,
        r.IsActive,
        r.CreatedAt
    );
}
