using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Services;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NutritionistsController : ControllerBase
{
    private readonly NutritionistService _nutritionistService;

    public NutritionistsController(NutritionistService nutritionistService)
    {
        _nutritionistService = nutritionistService;
    }

    [HttpGet]
    public async Task<ActionResult<List<NutritionistDto>>> GetAll()
    {
        var list = await _nutritionistService.GetAllAsync();
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NutritionistDto>> GetById(Guid id)
    {
        var item = await _nutritionistService.GetByIdAsync(id);
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<NutritionistDto>> Create([FromBody] CreateNutritionistRequest request)
    {
        var created = await _nutritionistService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NutritionistDto>> Update(Guid id, [FromBody] UpdateNutritionistRequest request)
    {
        var updated = await _nutritionistService.UpdateAsync(id, request);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _nutritionistService.DeleteAsync(id);
        return NoContent();
    }
}
