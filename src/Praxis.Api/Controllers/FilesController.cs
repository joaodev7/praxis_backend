using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Praxis.Application.DTOs;
using Praxis.Application.Services;

namespace Praxis.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly FileService _fileService;

    public FilesController(FileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Gera uma presigned URL temporária para upload direto do frontend para o Cloudflare R2.
    /// </summary>
    [HttpPost("upload-url")]
    [ProducesResponseType(typeof(GenerateUploadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GenerateUploadUrlResponse>> GenerateUploadUrl([FromBody] GenerateUploadUrlRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _fileService.GenerateUploadUrlAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Confirma a conclusão do upload direto no Cloudflare R2 e atualiza o status para Uploaded.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(CompleteUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompleteUploadResponse>> CompleteUpload(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _fileService.CompleteUploadAsync(id, cancellationToken);
            return Ok(response);
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
    /// Gera uma presigned URL temporária para visualização/download privado do arquivo do Cloudflare R2.
    /// </summary>
    [HttpGet("{id:guid}/download-url")]
    [ProducesResponseType(typeof(FileDownloadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FileDownloadUrlResponse>> GetDownloadUrl(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _fileService.GetDownloadUrlAsync(id, cancellationToken);
            return Ok(response);
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
    /// Obtém os metadados cadastrados do arquivo no PostgreSQL.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FileDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var file = await _fileService.GetByIdAsync(id, cancellationToken);
            return Ok(file);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lista os arquivos associados a um cliente específico dentro da organização.
    /// </summary>
    [HttpGet("client/{clientId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<FileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FileDto>>> ListByClient(Guid clientId, CancellationToken cancellationToken)
    {
        var files = await _fileService.ListByClientAsync(clientId, cancellationToken);
        return Ok(files);
    }

    /// <summary>
    /// Exclui o arquivo do Cloudflare R2 e aplica soft delete no banco de dados.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _fileService.DeleteFileAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
