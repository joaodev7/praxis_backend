using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Services;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EvidencesController : ControllerBase
{
    private readonly EvidenceService _evidenceService;

    public EvidencesController(EvidenceService evidenceService)
    {
        _evidenceService = evidenceService;
    }

    [HttpPost]
    public async Task<ActionResult<EvidenceDto>> Create([FromBody] CreateEvidenceRequest request)
    {
        var created = await _evidenceService.CreateAsync(request);
        return Ok(created);
    }

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp", "application/pdf" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    [HttpPost("upload")]
    public async Task<ActionResult<object>> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Nenhum arquivo enviado." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "O tamanho do arquivo excede o limite máximo de 10 MB." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new { message = $"Formato de arquivo não permitido ({extension}). Formatos aceitos: {string.Join(", ", AllowedExtensions)}" });

        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
        if (!AllowedContentTypes.Contains(contentType))
            return BadRequest(new { message = $"Tipo MIME inválido ({file.ContentType})." });

        using var stream = file.OpenReadStream();
        var url = await _evidenceService.UploadFileAsync(stream, file.FileName, contentType);

        return Ok(new { url, fileName = file.FileName, size = file.Length });
    }
}
