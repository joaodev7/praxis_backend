using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Praxis.Domain.Services;

namespace Praxis.Application.Services;

public class FoodLabelService : IFoodLabelService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidityCalculator _validityCalculator;

    public FoodLabelService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IValidityCalculator validityCalculator)
    {
        _context = context;
        _currentUser = currentUser;
        _validityCalculator = validityCalculator;
    }

    public async Task<List<FoodLabelListDto>> GetAllAsync(FoodLabelFilterParams filter)
    {
        var query = _context.FoodLabels
            .Include(l => l.Unit)
            .Include(l => l.Product)
            .Where(l => !l.IsDeleted);

        if (filter.UnitId.HasValue)
            query = query.Where(l => l.UnitId == filter.UnitId.Value);

        if (filter.ProductId.HasValue)
            query = query.Where(l => l.ProductId == filter.ProductId.Value);

        if (filter.Status.HasValue)
            query = query.Where(l => l.Status == filter.Status.Value);

        if (filter.LabelType.HasValue)
            query = query.Where(l => l.LabelType == filter.LabelType.Value);

        if (filter.OperationType.HasValue)
            query = query.Where(l => l.OperationType == filter.OperationType.Value);

        if (!string.IsNullOrWhiteSpace(filter.BatchCode))
            query = query.Where(l => l.InternalBatchCode.Contains(filter.BatchCode.Trim()));

        if (filter.ExpirationFrom.HasValue)
            query = query.Where(l => (l.ManualExpirationDate ?? l.CalculatedExpirationDate) >= filter.ExpirationFrom.Value);

        if (filter.ExpirationTo.HasValue)
            query = query.Where(l => (l.ManualExpirationDate ?? l.CalculatedExpirationDate) <= filter.ExpirationTo.Value);

        if (filter.CreatedFrom.HasValue)
            query = query.Where(l => l.CreatedAt >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query = query.Where(l => l.CreatedAt <= filter.CreatedTo.Value);

        var now = DateTime.UtcNow;

        if (filter.OnlyExpired == true)
        {
            query = query.Where(l => l.Status == LabelStatus.Active && (l.ManualExpirationDate ?? l.CalculatedExpirationDate) < now);
        }

        if (filter.OnlyExpiringSoon == true)
        {
            var tomorrow = now.AddDays(1);
            query = query.Where(l => l.Status == LabelStatus.Active &&
                                     (l.ManualExpirationDate ?? l.CalculatedExpirationDate) >= now &&
                                     (l.ManualExpirationDate ?? l.CalculatedExpirationDate) <= tomorrow);
        }

        var list = await query
            .OrderBy(l => l.ManualExpirationDate ?? l.CalculatedExpirationDate)
            .Take(200)
            .ToListAsync();

        return list.Select(l => new FoodLabelListDto(
            l.Id,
            l.UnitId,
            l.Unit?.Name ?? "Unidade",
            l.ProductId,
            l.Product?.Name ?? "Alimento",
            l.InternalBatchCode,
            l.LabelType,
            l.OperationType,
            l.Status,
            l.ValidityStartAt,
            l.EffectiveExpirationDate,
            l.IsExpired,
            l.StorageCondition,
            l.StorageTemperatureMax,
            l.PublicToken,
            l.PrintCount,
            l.CreatedAt
        )).ToList();
    }

    public async Task<FoodLabelDto> GetByIdAsync(Guid id)
    {
        var l = await _context.FoodLabels
            .Include(x => x.Unit)
            .Include(x => x.Product)
            .Include(x => x.ProductBatch)
            .Include(x => x.ValidityRule)
            .Include(x => x.CreatedByUser)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (l == null) throw new KeyNotFoundException("Etiqueta não encontrada.");

        return MapToDto(l);
    }

    public async Task<FoodLabelDto> CreateAsync(CreateFoodLabelRequest request)
    {
        var tenantId = _currentUser.TenantId ?? Guid.Empty;
        var userId = _currentUser.UserId ?? Guid.Empty;

        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == request.UnitId && !u.IsDeleted);
        if (unit == null) throw new KeyNotFoundException("Unidade operacional não encontrada.");

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted);
        if (product == null) throw new KeyNotFoundException("Produto não encontrado.");

        DateTime? originalExpDate = null;
        if (request.ProductBatchId.HasValue)
        {
            var batch = await _context.ProductBatches.FirstOrDefaultAsync(b => b.Id == request.ProductBatchId.Value && !b.IsDeleted);
            if (batch == null) throw new KeyNotFoundException("Lote do produto não encontrado.");
            originalExpDate = batch.OriginalExpirationDate;
        }

        // Buscar regras ativas
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
            request.StorageTemperatureMax,
            activeRules
        );

        DateTime operationDate = request.OperationDateTime ?? DateTime.UtcNow;

        var calcResult = _validityCalculator.Calculate(
            operationDate,
            matchedRule,
            request.ManualExpirationDate,
            request.ManualJustification,
            originalExpDate
        );

        if (!calcResult.RuleMatched && !request.ManualExpirationDate.HasValue)
        {
            throw new InvalidOperationException(
                "Nenhuma regra de validade aplicável encontrada para este alimento/condição. É obrigatório informar a data de validade manual acompanhada de justificativa técnica.");
        }

        // Gerar código de lote interno sequencial único
        string batchCode = await GenerateBatchCodeAsync(tenantId, request.OperationType, operationDate);

        // Gerar token público seguro
        string publicToken = GeneratePublicToken();

        var label = new FoodLabel
        {
            TenantId = tenantId,
            UnitId = request.UnitId,
            ProductId = request.ProductId,
            ProductBatchId = request.ProductBatchId,
            ValidityRuleId = matchedRule?.Id,
            LabelType = request.LabelType,
            OperationType = request.OperationType,
            Status = LabelStatus.Active,
            Description = string.IsNullOrWhiteSpace(request.Description) ? product.Name : request.Description.Trim(),
            InternalBatchCode = batchCode,
            ValidityStartAt = operationDate,
            CalculatedExpirationDate = calcResult.ExpirationDate,
            ManualExpirationDate = request.ManualExpirationDate,
            ValiditySource = calcResult.Source,
            ValidityJustification = request.ManualJustification?.Trim() ?? calcResult.Explanation,
            StorageCondition = request.StorageCondition,
            StorageTemperatureMin = request.StorageTemperatureMin,
            StorageTemperatureMax = request.StorageTemperatureMax,
            StorageInstructions = request.StorageInstructions?.Trim(),
            PublicToken = publicToken,
            CreatedByUserId = userId,
            PrintCount = request.PrintCopies > 0 ? request.PrintCopies : 1,
            LastPrintedAt = DateTime.UtcNow
        };

        // Mapear datas específicas de acordo com a operação
        switch (request.OperationType)
        {
            case LabelOperationType.Preparation:
                label.PreparedAt = operationDate;
                break;
            case LabelOperationType.Opening:
                label.OpenedAt = operationDate;
                break;
            case LabelOperationType.Portioning:
                label.PortionedAt = operationDate;
                break;
            case LabelOperationType.Defrosting:
            case LabelOperationType.PrePreparation:
            case LabelOperationType.Storage:
                label.PreparedAt = operationDate;
                break;
        }

        _context.FoodLabels.Add(label);

        // Auditoria
        _context.FoodLabelAudits.Add(new FoodLabelAudit
        {
            TenantId = tenantId,
            LabelId = label.Id,
            Action = "Created",
            UserId = userId,
            Details = $"Etiqueta criada com lote {batchCode}. Validade calculada para {label.EffectiveExpirationDate:dd/MM/yyyy HH:mm} via {label.ValiditySource}."
        });

        // Registro de impressão inicial
        if (request.PrintCopies > 0)
        {
            _context.LabelPrints.Add(new LabelPrint
            {
                TenantId = tenantId,
                LabelId = label.Id,
                PrintedByUserId = userId,
                PrintedAt = DateTime.UtcNow,
                Quantity = request.PrintCopies,
                TemplateUsed = request.TemplateType ?? "Thermal80x50"
            });
        }

        await _context.SaveChangesAsync();

        return await GetByIdAsync(label.Id);
    }

    public async Task<FoodLabelDto> CancelAsync(Guid id, CancelFoodLabelRequest request)
    {
        var label = await _context.FoodLabels.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
        if (label == null) throw new KeyNotFoundException("Etiqueta não encontrada.");

        if (label.Status == LabelStatus.Cancelled)
            throw new InvalidOperationException("Esta etiqueta já está cancelada.");

        var userId = _currentUser.UserId ?? Guid.Empty;
        var oldStatus = label.Status.ToString();

        label.Status = LabelStatus.Cancelled;
        label.CancelledByUserId = userId;
        label.CancelledAt = DateTime.UtcNow;
        label.CancellationReason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Cancelamento solicitado pelo operador"
            : request.Reason.Trim();

        _context.FoodLabelAudits.Add(new FoodLabelAudit
        {
            TenantId = label.TenantId,
            LabelId = label.Id,
            Action = "Cancelled",
            OldValue = oldStatus,
            NewValue = LabelStatus.Cancelled.ToString(),
            UserId = userId,
            Details = $"Motivo do cancelamento: {label.CancellationReason}"
        });

        await _context.SaveChangesAsync();

        return await GetByIdAsync(label.Id);
    }

    public async Task<FoodLabelDto> DiscardAsync(Guid id, DiscardFoodLabelRequest request)
    {
        var label = await _context.FoodLabels.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
        if (label == null) throw new KeyNotFoundException("Etiqueta não encontrada.");

        var userId = _currentUser.UserId ?? Guid.Empty;
        var oldStatus = label.Status.ToString();

        label.Status = LabelStatus.Discarded;
        label.DiscardedByUserId = userId;
        label.DiscardedAt = DateTime.UtcNow;
        label.DiscardReason = request.Reason.Trim();
        label.DiscardQuantity = request.Quantity;
        label.DiscardUnit = request.Unit?.Trim() ?? "unidade";

        _context.FoodLabelAudits.Add(new FoodLabelAudit
        {
            TenantId = label.TenantId,
            LabelId = label.Id,
            Action = "Discarded",
            OldValue = oldStatus,
            NewValue = LabelStatus.Discarded.ToString(),
            UserId = userId,
            Details = $"Alimento descartado: {request.Quantity} {request.Unit}. Motivo: {request.Reason}"
        });

        await _context.SaveChangesAsync();

        return await GetByIdAsync(label.Id);
    }

    public async Task<FoodLabelDto> ReprintAsync(Guid id, ReprintFoodLabelRequest request)
    {
        var label = await _context.FoodLabels.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
        if (label == null) throw new KeyNotFoundException("Etiqueta não encontrada.");

        if (label.Status == LabelStatus.Cancelled)
            throw new InvalidOperationException("Não é permitido reimprimir uma etiqueta cancelada.");

        var userId = _currentUser.UserId ?? Guid.Empty;
        int qty = request.Quantity > 0 ? request.Quantity : 1;

        label.PrintCount += qty;
        label.LastPrintedAt = DateTime.UtcNow;

        _context.LabelPrints.Add(new LabelPrint
        {
            TenantId = label.TenantId,
            LabelId = label.Id,
            PrintedByUserId = userId,
            PrintedAt = DateTime.UtcNow,
            Quantity = qty,
            PrinterName = request.PrinterName,
            TemplateUsed = request.TemplateUsed ?? "Thermal80x50"
        });

        _context.FoodLabelAudits.Add(new FoodLabelAudit
        {
            TenantId = label.TenantId,
            LabelId = label.Id,
            Action = "Reprinted",
            UserId = userId,
            Details = $"Reimpressão de {qty} cópias (Total: {label.PrintCount})."
        });

        await _context.SaveChangesAsync();

        return await GetByIdAsync(label.Id);
    }

    public async Task<FoodLabelDashboardDto> GetDashboardStatsAsync(Guid? unitId = null)
    {
        var query = _context.FoodLabels.Where(l => !l.IsDeleted);

        if (unitId.HasValue)
            query = query.Where(l => l.UnitId == unitId.Value);

        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1).AddTicks(-1);
        var tomorrowStart = todayStart.AddDays(1);
        var tomorrowEnd = tomorrowStart.AddDays(1).AddTicks(-1);

        var labels = await query
            .Select(l => new
            {
                l.Status,
                ExpirationDate = l.ManualExpirationDate ?? l.CalculatedExpirationDate,
                l.PrintCount,
                l.ValiditySource
            })
            .ToListAsync();

        int totalActive = labels.Count(l => l.Status == LabelStatus.Active && l.ExpirationDate >= now);
        int expiresToday = labels.Count(l => l.Status == LabelStatus.Active && l.ExpirationDate >= todayStart && l.ExpirationDate <= todayEnd);
        int expiresTomorrow = labels.Count(l => l.Status == LabelStatus.Active && l.ExpirationDate >= tomorrowStart && l.ExpirationDate <= tomorrowEnd);
        int expired = labels.Count(l => (l.Status == LabelStatus.Active && l.ExpirationDate < now) || l.Status == LabelStatus.Expired);
        int cancelled = labels.Count(l => l.Status == LabelStatus.Cancelled);
        int discarded = labels.Count(l => l.Status == LabelStatus.Discarded);
        int totalPrinted = labels.Sum(l => l.PrintCount);
        int withoutRule = labels.Count(l => l.Status == LabelStatus.Active && l.ValiditySource == ValiditySource.Manual);

        return new FoodLabelDashboardDto(
            TotalActive: totalActive,
            ExpiresToday: expiresToday,
            ExpiresTomorrow: expiresTomorrow,
            Expired: expired,
            Cancelled: cancelled,
            Discarded: discarded,
            TotalPrinted: totalPrinted,
            WithoutConfiguredRule: withoutRule
        );
    }

    public async Task<FoodLabelPublicDto> GetPublicByTokenAsync(string publicToken)
    {
        if (string.IsNullOrWhiteSpace(publicToken))
            throw new ArgumentException("Token público inválido.");

        // Consulta pública desativa o filtro de TenantId via IgnoreQueryFilters
        var label = await _context.FoodLabels
            .IgnoreQueryFilters()
            .Include(l => l.Product)
            .Include(l => l.Unit)
            .Include(l => l.ValidityRule)
            .FirstOrDefaultAsync(l => l.PublicToken == publicToken.Trim() && !l.IsDeleted);

        if (label == null)
            throw new KeyNotFoundException("Etiqueta não encontrada ou token expirado.");

        string operationName = label.OperationType switch
        {
            LabelOperationType.Preparation => "Preparo",
            LabelOperationType.Opening => "Abertura de Embalagem",
            LabelOperationType.Portioning => "Fracionamento",
            LabelOperationType.Defrosting => "Descongelamento",
            LabelOperationType.PrePreparation => "Pré-preparo",
            LabelOperationType.Storage => "Armazenamento",
            _ => label.OperationType.ToString()
        };

        string storageCond = label.StorageCondition switch
        {
            StorageCondition.Ambient => "Temperatura Ambiente",
            StorageCondition.Refrigerated => "Refrigerado",
            StorageCondition.Frozen => "Congelado",
            StorageCondition.Heated => "Aquecido / Estufa",
            _ => "Outro"
        };

        return new FoodLabelPublicDto(
            ProductName: label.Product?.Name ?? label.Description,
            InternalBatchCode: label.InternalBatchCode,
            OperationName: operationName,
            ValidityStartAt: label.ValidityStartAt,
            ExpirationDate: label.EffectiveExpirationDate,
            IsExpired: label.IsExpired,
            StorageCondition: storageCond,
            StorageTemperatureMax: label.StorageTemperatureMax,
            StorageInstructions: label.StorageInstructions,
            UnitName: label.Unit?.Name ?? "Unidade",
            Status: label.Status.ToString(),
            TechnicalBasis: label.ValidityRule?.RegulatoryReference ?? label.ValidityJustification
        );
    }

    public async Task<List<LabelTemplateDto>> GetTemplatesAsync()
    {
        var templates = await _context.LabelTemplates
            .Where(t => t.IsActive)
            .ToListAsync();

        if (!templates.Any())
        {
            // Retorna templates padrão do PRAXIS
            return new List<LabelTemplateDto>
            {
                new(Guid.NewGuid(), "Etiqueta Térmica 50x30 mm (Compacta)", "Thermal50x30", 50, 30, true, true, false),
                new(Guid.NewGuid(), "Etiqueta Térmica 80x50 mm (Padrão RT)", "Thermal80x50", 80, 50, true, true, true),
                new(Guid.NewGuid(), "Folha A4 (21 Etiquetas 70x42 mm)", "SheetA4", 70, 42.4m, true, true, false)
            };
        }

        return templates.Select(t => new LabelTemplateDto(
            t.Id,
            t.Name,
            t.TemplateType,
            t.WidthMm,
            t.HeightMm,
            t.IncludeQrCode,
            t.IncludeLogo,
            t.IsDefault
        )).ToList();
    }

    private async Task<string> GenerateBatchCodeAsync(Guid tenantId, LabelOperationType opType, DateTime date)
    {
        string prefix = opType switch
        {
            LabelOperationType.Preparation => "PREP",
            LabelOperationType.Opening => "OPEN",
            LabelOperationType.Portioning => "FRAC",
            LabelOperationType.Defrosting => "DESC",
            LabelOperationType.PrePreparation => "PREP",
            LabelOperationType.Storage => "ARMA",
            _ => "LOTE"
        };

        string dateStr = date.ToString("yyyyMMdd");
        string searchPrefix = $"{prefix}-{dateStr}-";

        // Obter contagem do dia para este tenant
        int countToday = await _context.FoodLabels
            .CountAsync(l => l.TenantId == tenantId && l.InternalBatchCode.StartsWith(searchPrefix));

        int nextSeq = countToday + 1;
        return $"{prefix}-{dateStr}-{nextSeq:D4}";
    }

    private static string GeneratePublicToken()
    {
        // 16 caracteres alfanuméricos seguros
        byte[] bytes = new byte[12];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static FoodLabelDto MapToDto(FoodLabel l) => new(
        l.Id,
        l.TenantId,
        l.UnitId,
        l.Unit?.Name ?? "Unidade",
        l.ProductId,
        l.Product?.Name ?? l.Description,
        l.ProductBatchId,
        l.ProductBatch?.BatchCode,
        l.ValidityRuleId,
        l.ValidityRule?.Name,
        l.LabelType,
        l.OperationType,
        l.Status,
        l.Description,
        l.InternalBatchCode,
        l.ManufacturedAt,
        l.PreparedAt,
        l.OpenedAt,
        l.PortionedAt,
        l.ValidityStartAt,
        l.CalculatedExpirationDate,
        l.ManualExpirationDate,
        l.EffectiveExpirationDate,
        l.IsExpired,
        l.ValiditySource,
        l.ValidityJustification,
        l.StorageCondition,
        l.StorageTemperatureMin,
        l.StorageTemperatureMax,
        l.StorageInstructions,
        l.PublicToken,
        l.PrintCount,
        l.LastPrintedAt,
        l.CreatedByUserId,
        l.CreatedByUser?.Name ?? "Usuário",
        l.CreatedAt,
        l.CancelledAt,
        l.CancellationReason,
        l.DiscardedAt,
        l.DiscardReason,
        l.DiscardQuantity,
        l.DiscardUnit
    );
}
