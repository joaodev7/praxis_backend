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

    [HttpPost("upload")]
    public async Task<ActionResult<object>> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Nenhum arquivo enviado.");

        using var stream = file.OpenReadStream();
        var url = await _evidenceService.UploadFileAsync(stream, file.FileName, file.ContentType);

        return Ok(new { url, fileName = file.FileName, size = file.Length });
    }
}
