using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Services;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UnitsController : ControllerBase
{
    private readonly UnitService _unitService;

    public UnitsController(UnitService unitService)
    {
        _unitService = unitService;
    }

    [HttpGet]
    public async Task<ActionResult<List<UnitDto>>> GetAll([FromQuery] Guid? clientCompanyId)
    {
        var list = await _unitService.GetAllAsync(clientCompanyId);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UnitDto>> GetById(Guid id)
    {
        var item = await _unitService.GetByIdAsync(id);
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<UnitDto>> Create([FromBody] CreateUnitRequest request)
    {
        var created = await _unitService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UnitDto>> Update(Guid id, [FromBody] UpdateUnitRequest request)
    {
        var updated = await _unitService.UpdateAsync(id, request);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _unitService.DeleteAsync(id);
        return NoContent();
    }
}
