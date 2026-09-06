using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Domain.Enums;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ActionPlansController : ControllerBase
{
    private readonly IActionPlanService _actionPlanService;

    public ActionPlansController(IActionPlanService actionPlanService)
    {
        _actionPlanService = actionPlanService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ActionPlanDto>>> GetAll(
        [FromQuery] Guid? nonConformityId,
        [FromQuery] Guid? visitId,
        [FromQuery] Guid? unitId,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? responsibleUserId,
        [FromQuery] ActionItemStatus? status,
        [FromQuery] ActionPlanPriority? priority,
        [FromQuery] bool? isLate,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var result = await _actionPlanService.GetAllAsync(
            nonConformityId,
            visitId,
            unitId,
            clientId,
            responsibleUserId,
            status,
            priority,
            isLate,
            startDate,
            endDate,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ActionPlanDashboardDto>> GetDashboard(
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? unitId,
        CancellationToken cancellationToken)
    {
        var result = await _actionPlanService.GetDashboardAsync(clientId, unitId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActionPlanDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionPlanService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ActionPlanDto>> Create([FromBody] CreateActionPlanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionPlanService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ActionPlanDto>> Update(Guid id, [FromBody] UpdateActionPlanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionPlanService.UpdateAsync(id, request, cancellationToken);
            return Ok(result);
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
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ActionPlanDto>> ChangeStatus(Guid id, [FromBody] ChangeActionPlanStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionPlanService.ChangeStatusAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/validate")]
    public async Task<ActionResult<ActionPlanDto>> Validate(Guid id, [FromBody] ValidateActionPlanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionPlanService.ValidateAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<ActionResult<ActionPlanDto>> Cancel(Guid id, [FromBody] CancelActionPlanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionPlanService.CancelAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/evidences")]
    public async Task<ActionResult<ActionPlanEvidenceDto>> UploadEvidence(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Nenhum arquivo enviado ou arquivo está vazio." });

        try
        {
            using var stream = file.OpenReadStream();
            var evidence = await _actionPlanService.AddEvidenceAsync(
                id,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);

            return Created($"/api/action-plans/{id}/evidences/{evidence.Id}", evidence);
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

    [HttpGet("{id:guid}/evidences")]
    public async Task<ActionResult<List<ActionPlanEvidenceDto>>> GetEvidences(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var list = await _actionPlanService.GetEvidencesAsync(id, cancellationToken);
            return Ok(list);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/evidences/{evidenceId:guid}")]
    public async Task<ActionResult> DeleteEvidence(Guid id, Guid evidenceId, CancellationToken cancellationToken)
    {
        try
        {
            await _actionPlanService.DeleteEvidenceAsync(evidenceId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

[Authorize]
[ApiController]
[Route("api/action-plan-evidences")]
public class ActionPlanEvidencesController : ControllerBase
{
    private readonly IActionPlanService _actionPlanService;

    public ActionPlanEvidencesController(IActionPlanService actionPlanService)
    {
        _actionPlanService = actionPlanService;
    }

    [HttpDelete("{evidenceId:guid}")]
    public async Task<ActionResult> DeleteEvidence(Guid evidenceId, CancellationToken cancellationToken)
    {
        try
        {
            await _actionPlanService.DeleteEvidenceAsync(evidenceId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
