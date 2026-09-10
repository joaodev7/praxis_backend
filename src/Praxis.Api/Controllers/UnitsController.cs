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
        try
        {
            var item = await _unitService.GetByIdAsync(id);
            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<UnitDto>> Create([FromBody] CreateUnitRequest request)
    {
        try
        {
            var created = await _unitService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UnitDto>> Update(Guid id, [FromBody] UpdateUnitRequest request)
    {
        try
        {
            var updated = await _unitService.UpdateAsync(id, request);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _unitService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Aloca / vincula um nutricionista à unidade especificada.
    /// </summary>
    [HttpPost("{id:guid}/nutritionists/{nutritionistId:guid}")]
    public async Task<ActionResult<UnitDto>> AllocateNutritionist(Guid id, Guid nutritionistId)
    {
        try
        {
            var updated = await _unitService.AllocateNutritionistAsync(id, nutritionistId);
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

    /// <summary>
    /// Desaloca / remove o vínculo de um nutricionista da unidade especificada.
    /// </summary>
    [HttpDelete("{id:guid}/nutritionists/{nutritionistId:guid}")]
    public async Task<ActionResult<UnitDto>> DeallocateNutritionist(Guid id, Guid nutritionistId)
    {
        try
        {
            var updated = await _unitService.DeallocateNutritionistAsync(id, nutritionistId);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
