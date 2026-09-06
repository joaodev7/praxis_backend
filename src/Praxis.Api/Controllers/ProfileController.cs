using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Services;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profileService;

    public ProfileController(ProfileService profileService)
    {
        _profileService = profileService;
    }

    /// <summary>
    /// Obtém os dados do perfil do usuário autenticado.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await _profileService.GetProfileAsync(cancellationToken);
        return Ok(profile);
    }

    /// <summary>
    /// Atualiza as informações de perfil (nome e data de nascimento) do usuário autenticado.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _profileService.UpdateProfileAsync(request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Realiza o upload da foto de perfil para o Cloudflare R2 e atualiza a referência no cadastro.
    /// </summary>
    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProfilePhotoUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfilePhotoUploadResponse>> UploadPhoto(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Arquivo de imagem não fornecido." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _profileService.UploadPhotoAsync(
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Remove a foto de perfil do usuário autenticado do Cloudflare R2 e limpa a referência cadastral.
    /// </summary>
    [HttpDelete("photo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeletePhoto(CancellationToken cancellationToken)
    {
        await _profileService.DeletePhotoAsync(cancellationToken);
        return NoContent();
    }
}
