using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Enums;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VisitsController : ControllerBase
{
    private readonly VisitService _visitService;
    private readonly IPdfReportService _pdfReportService;
    private readonly IApplicationDbContext _context;

    public VisitsController(VisitService visitService, IPdfReportService pdfReportService, IApplicationDbContext context)
    {
        _visitService = visitService;
        _pdfReportService = pdfReportService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<VisitDto>>> GetAll([FromQuery] Guid? nutritionistId, [FromQuery] Guid? unitId, [FromQuery] VisitStatus? status)
    {
        var list = await _visitService.GetAllAsync(nutritionistId, unitId, status);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VisitDetailDto>> GetById(Guid id)
    {
        var item = await _visitService.GetByIdAsync(id);
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<VisitDto>> Create([FromBody] CreateVisitRequest request)
    {
        var created = await _visitService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<VisitDetailDto>> Start(Guid id)
    {
        var item = await _visitService.StartVisitAsync(id);
        return Ok(item);
    }

    [HttpPost("{id:guid}/finish")]
    public async Task<ActionResult<VisitDetailDto>> Finish(Guid id, [FromBody] FinishVisitRequest request)
    {
        var item = await _visitService.FinishVisitAsync(id, request);
        return Ok(item);
    }

    [HttpGet("{id:guid}/report")]
    public async Task<IActionResult> DownloadReportPdf(Guid id)
    {
        var visit = await _context.Visits
            .Include(v => v.Unit)
                .ThenInclude(u => u!.ClientCompany)
            .Include(v => v.Nutritionist)
                .ThenInclude(n => n!.User)
            .Include(v => v.Checklist)
            .Include(v => v.Items)
                .ThenInclude(i => i.ChecklistItem)
            .Include(v => v.NonConformities)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);

        if (visit == null) return NotFound("Visita não encontrada.");

        var pdfBytes = _pdfReportService.GenerateVisitReportPdf(visit);
        return File(pdfBytes, "application/pdf", $"relatorio-visita-{visit.Unit?.Name ?? "unidade"}-{visit.ScheduledAt:yyyyMMdd}.pdf");
    }
}
