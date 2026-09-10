using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Enums;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/food-labels")]
public class FoodLabelsController : ControllerBase
{
    private readonly IFoodLabelService _foodLabelService;
    private readonly ILabelPdfService _labelPdfService;
    private readonly IApplicationDbContext _context;

    public FoodLabelsController(
        IFoodLabelService foodLabelService,
        ILabelPdfService labelPdfService,
        IApplicationDbContext context)
    {
        _foodLabelService = foodLabelService;
        _labelPdfService = labelPdfService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<FoodLabelListDto>>> GetAll(
        [FromQuery] Guid? unitId,
        [FromQuery] Guid? productId,
        [FromQuery] LabelStatus? status,
        [FromQuery] LabelType? labelType,
        [FromQuery] LabelOperationType? operationType,
        [FromQuery] string? batchCode,
        [FromQuery] DateTime? expirationFrom,
        [FromQuery] DateTime? expirationTo,
        [FromQuery] DateTime? createdFrom,
        [FromQuery] DateTime? createdTo,
        [FromQuery] bool? onlyExpired,
        [FromQuery] bool? onlyExpiringSoon)
    {
        var filter = new FoodLabelFilterParams(
            unitId,
            productId,
            status,
            labelType,
            operationType,
            batchCode,
            expirationFrom,
            expirationTo,
            createdFrom,
            createdTo,
            onlyExpired,
            onlyExpiringSoon
        );

        var list = await _foodLabelService.GetAllAsync(filter);
        return Ok(list);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<FoodLabelDashboardDto>> GetDashboard([FromQuery] Guid? unitId)
    {
        var stats = await _foodLabelService.GetDashboardStatsAsync(unitId);
        return Ok(stats);
    }

    [HttpGet("templates")]
    public async Task<ActionResult<List<LabelTemplateDto>>> GetTemplates()
    {
        var templates = await _foodLabelService.GetTemplatesAsync();
        return Ok(templates);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FoodLabelDto>> GetById(Guid id)
    {
        try
        {
            var label = await _foodLabelService.GetByIdAsync(id);
            return Ok(label);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<FoodLabelDto>> Create([FromBody] CreateFoodLabelRequest request)
    {
        try
        {
            var created = await _foodLabelService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<FoodLabelDto>> Cancel(Guid id, [FromBody] CancelFoodLabelRequest request)
    {
        try
        {
            var updated = await _foodLabelService.CancelAsync(id, request);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/discard")]
    public async Task<ActionResult<FoodLabelDto>> Discard(Guid id, [FromBody] DiscardFoodLabelRequest request)
    {
        try
        {
            var updated = await _foodLabelService.DiscardAsync(id, request);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reprint")]
    public async Task<ActionResult<FoodLabelDto>> Reprint(Guid id, [FromBody] ReprintFoodLabelRequest request)
    {
        try
        {
            var updated = await _foodLabelService.ReprintAsync(id, request);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(
        Guid id,
        [FromQuery] string templateType = "Thermal80x50",
        [FromQuery] int copies = 1)
    {
        var label = await _context.FoodLabels
            .Include(l => l.Product)
            .Include(l => l.Unit)
            .Include(l => l.CreatedByUser)
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

        if (label == null)
            return NotFound(new { message = "Etiqueta não encontrada." });

        var pdfBytes = _labelPdfService.GenerateSingleLabelPdf(label, templateType, copies);
        string filename = $"etiqueta-{label.InternalBatchCode}.pdf";

        return File(pdfBytes, "application/pdf", filename);
    }

    [HttpPost("pdf")]
    public async Task<IActionResult> DownloadBulkPdf([FromBody] PrintLabelsRequest request)
    {
        if (request.LabelIds == null || !request.LabelIds.Any())
            return BadRequest(new { message = "Nenhuma etiqueta informada." });

        var labels = await _context.FoodLabels
            .Include(l => l.Product)
            .Include(l => l.Unit)
            .Include(l => l.CreatedByUser)
            .Where(l => request.LabelIds.Contains(l.Id) && !l.IsDeleted)
            .ToListAsync();

        if (!labels.Any())
            return NotFound(new { message = "Nenhuma etiqueta correspondente encontrada." });

        var pdfBytes = _labelPdfService.GenerateBulkLabelsPdf(labels, request.TemplateType, request.CopiesPerLabel);
        string filename = $"etiquetas-lote-{DateTime.UtcNow:yyyyMMddHHmm}.pdf";

        return File(pdfBytes, "application/pdf", filename);
    }
}
