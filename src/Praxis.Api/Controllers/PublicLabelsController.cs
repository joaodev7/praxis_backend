using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;

namespace Praxis.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/public/labels")]
public class PublicLabelsController : ControllerBase
{
    private readonly IFoodLabelService _foodLabelService;

    public PublicLabelsController(IFoodLabelService foodLabelService)
    {
        _foodLabelService = foodLabelService;
    }

    [HttpGet("{token}")]
    public async Task<ActionResult<FoodLabelPublicDto>> VerifyByToken(string token)
    {
        try
        {
            var data = await _foodLabelService.GetPublicByTokenAsync(token);
            return Ok(data);
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
}
