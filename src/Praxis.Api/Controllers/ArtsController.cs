using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Services;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ArtsController : ControllerBase
{
    private readonly ArtService _artService;

    public ArtsController(ArtService artService)
    {
        _artService = artService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ArtDto>>> GetAll()
    {
        var list = await _artService.GetAllAsync();
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ArtDto>> GetById(Guid id)
    {
        var item = await _artService.GetByIdAsync(id);
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ArtDto>> Create([FromBody] CreateArtRequest request)
    {
        var created = await _artService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ArtDto>> Update(Guid id, [FromBody] UpdateArtRequest request)
    {
        var updated = await _artService.UpdateAsync(id, request);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _artService.DeleteAsync(id);
        return NoContent();
    }
}
