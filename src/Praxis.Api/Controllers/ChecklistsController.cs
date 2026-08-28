using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Services;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChecklistsController : ControllerBase
{
    private readonly ChecklistService _checklistService;

    public ChecklistsController(ChecklistService checklistService)
    {
        _checklistService = checklistService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ChecklistDto>>> GetAll()
    {
        var list = await _checklistService.GetAllAsync();
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChecklistDto>> GetById(Guid id)
    {
        var item = await _checklistService.GetByIdAsync(id);
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ChecklistDto>> Create([FromBody] CreateChecklistRequest request)
    {
        var created = await _checklistService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ChecklistDto>> Update(Guid id, [FromBody] UpdateChecklistRequest request)
    {
        var updated = await _checklistService.UpdateAsync(id, request);
        return Ok(updated);
    }
}
