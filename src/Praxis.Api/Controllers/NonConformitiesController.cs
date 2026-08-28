using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Services;
using Praxis.Domain.Enums;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NonConformitiesController : ControllerBase
{
    private readonly NonConformityService _ncService;

    public NonConformitiesController(NonConformityService ncService)
    {
        _ncService = ncService;
    }

    [HttpGet]
    public async Task<ActionResult<List<NonConformityDto>>> GetAll([FromQuery] NonConformityStatus? status, [FromQuery] NonConformitySeverity? severity, [FromQuery] Guid? unitId)
    {
        var list = await _ncService.GetAllAsync(status, severity, unitId);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NonConformityDto>> GetById(Guid id)
    {
        var item = await _ncService.GetByIdAsync(id);
        return Ok(item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NonConformityDto>> Update(Guid id, [FromBody] UpdateNonConformityRequest request)
    {
        var updated = await _ncService.UpdateAsync(id, request);
        return Ok(updated);
    }

    [HttpPost("{id:guid}/actions")]
    public async Task<ActionResult<ActionItemDto>> AddAction(Guid id, [FromBody] CreateActionItemRequest request)
    {
        var created = await _ncService.AddActionItemAsync(id, request);
        return Ok(created);
    }

    [HttpPut("{id:guid}/actions/{actionId:guid}")]
    public async Task<ActionResult<ActionItemDto>> UpdateAction(Guid id, Guid actionId, [FromBody] UpdateActionItemRequest request)
    {
        var updated = await _ncService.UpdateActionItemAsync(id, actionId, request);
        return Ok(updated);
    }
}
