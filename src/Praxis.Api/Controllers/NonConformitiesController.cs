using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Enums;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
public class NonConformitiesController : ControllerBase
{
    private readonly NonConformityService _ncService;
    private readonly IActionPlanService _actionPlanService;

    public NonConformitiesController(NonConformityService ncService, IActionPlanService actionPlanService)
    {
        _ncService = ncService;
        _actionPlanService = actionPlanService;
    }

    [HttpGet("api/non-conformities")]
    public async Task<ActionResult<List<NonConformityDto>>> GetAll(
        [FromQuery] NonConformityStatus? status,
        [FromQuery] NonConformitySeverity? severity,
        [FromQuery] Guid? unitId,
        [FromQuery] Guid? visitId)
    {
        var list = await _ncService.GetAllAsync(status, severity, unitId, visitId);
        return Ok(list);
    }

    [HttpGet("api/visits/{visitId:guid}/non-conformities")]
    [HttpGet("api/audits/{visitId:guid}/non-conformities")]
    public async Task<ActionResult<List<NonConformityDto>>> GetByVisit(
        Guid visitId,
        [FromQuery] NonConformityStatus? status,
        [FromQuery] NonConformitySeverity? severity,
        [FromQuery] Guid? unitId)
    {
        var list = await _ncService.GetAllAsync(status, severity, unitId, visitId);
        return Ok(list);
    }

    [HttpPost("api/visits/{visitId:guid}/non-conformities")]
    [HttpPost("api/audits/{visitId:guid}/non-conformities")]
    public async Task<ActionResult<NonConformityDto>> CreateForVisit(Guid visitId, [FromBody] CreateNonConformityRequest request)
    {
        try
        {
            var created = await _ncService.CreateForVisitAsync(visitId, request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("api/non-conformities/{id:guid}")]
    public async Task<ActionResult<NonConformityDto>> GetById(Guid id)
    {
        try
        {
            var item = await _ncService.GetByIdAsync(id);
            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("api/non-conformities/{id:guid}")]
    public async Task<ActionResult<NonConformityDto>> Update(Guid id, [FromBody] UpdateNonConformityRequest request)
    {
        try
        {
            var updated = await _ncService.UpdateAsync(id, request);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("api/non-conformities/{id:guid}/action-plans")]
    public async Task<ActionResult<List<ActionPlanDto>>> GetActionPlans(Guid id, CancellationToken cancellationToken)
    {
        var list = await _actionPlanService.GetAllAsync(nonConformityId: id, cancellationToken: cancellationToken);
        return Ok(list);
    }

    [HttpPost("api/non-conformities/{id:guid}/action-plans")]
    public async Task<ActionResult<ActionPlanDto>> CreateActionPlan(Guid id, [FromBody] CreateActionPlanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var requestWithId = request with { NonConformityId = id };
            var created = await _actionPlanService.CreateAsync(requestWithId, cancellationToken);
            return Created($"/api/action-plans/{created.Id}", created);
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

    [HttpPost("api/non-conformities/{id:guid}/actions")]
    public async Task<ActionResult<ActionItemDto>> AddAction(Guid id, [FromBody] CreateActionItemRequest request)
    {
        var created = await _ncService.AddActionItemAsync(id, request);
        return Ok(created);
    }

    [HttpPut("api/non-conformities/{id:guid}/actions/{actionId:guid}")]
    public async Task<ActionResult<ActionItemDto>> UpdateAction(Guid id, Guid actionId, [FromBody] UpdateActionItemRequest request)
    {
        var updated = await _ncService.UpdateActionItemAsync(id, actionId, request);
        return Ok(updated);
    }
}
