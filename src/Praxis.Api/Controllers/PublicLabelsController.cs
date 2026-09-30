using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;

namespace Praxis.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/public/labels")]
public class PublicLabelsController : ControllerBase
{
    private readonly IFoodLabelService _foodLabelService;
    private readonly ILabelPdfService _labelPdfService;
    private readonly IApplicationDbContext _context;

    public PublicLabelsController(
        IFoodLabelService foodLabelService,
        ILabelPdfService labelPdfService,
        IApplicationDbContext context)
    {
        _foodLabelService = foodLabelService;
        _labelPdfService = labelPdfService;
        _context = context;
    }

    [HttpGet("{token}")]
    public async Task<ActionResult<FoodLabelPublicDto>> VerifyByToken(string token)
    {
        try
        {
            var data = await _foodLabelService.GetPublicByTokenAsync(token);
            return Ok(data);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{token}/pdf")]
    public async Task<IActionResult> DownloadByTokenPdf(
        string token,
        [FromQuery] string templateType = "Thermal80x50",
        [FromQuery] int copies = 1)
    {
        var label = await _context.FoodLabels
            .Include(l => l.Product)
            .Include(l => l.Unit)
            .Include(l => l.CreatedByUser)
            .FirstOrDefaultAsync(l => l.PublicToken == token && !l.IsDeleted);

        if (label == null)
            return NotFound(new { message = "Etiqueta não encontrada." });

        var pdfBytes = _labelPdfService.GenerateSingleLabelPdf(label, templateType, copies);
        string filename = $"etiqueta-{label.InternalBatchCode}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }
}
