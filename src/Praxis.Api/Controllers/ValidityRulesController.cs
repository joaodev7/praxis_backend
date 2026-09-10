using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/validity-rules")]
public class ValidityRulesController : ControllerBase
{
    private readonly IValidityRuleService _validityRuleService;

    public ValidityRulesController(IValidityRuleService validityRuleService)
    {
        _validityRuleService = validityRuleService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ValidityRuleDto>>> GetAll([FromQuery] Guid? unitId, [FromQuery] string? category)
    {
        var list = await _validityRuleService.GetAllAsync(unitId, category);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ValidityRuleDto>> GetById(Guid id)
    {
        try
        {
            var rule = await _validityRuleService.GetByIdAsync(id);
            return Ok(rule);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ValidityRuleDto>> Create([FromBody] CreateValidityRuleRequest request)
    {
        try
        {
            var created = await _validityRuleService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ValidityRuleDto>> Update(Guid id, [FromBody] UpdateValidityRuleRequest request)
    {
        try
        {
            var updated = await _validityRuleService.UpdateAsync(id, request);
            return Ok(updated);
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

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _validityRuleService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("simulate")]
    public async Task<ActionResult<SimulateValidityResponse>> Simulate([FromBody] SimulateValidityRequest request)
    {
        try
        {
            var result = await _validityRuleService.SimulateValidityAsync(request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
