using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Praxis.Application.Interfaces;
using Praxis.Infrastructure.Storage;
using System.Text;

namespace Praxis.Api.Controllers;

[ApiController]
[Route("api/admin/storage")]
public class StorageDiagnosticController : ControllerBase
{
    private readonly IAmazonS3 _s3Client;
    private readonly R2Options _options;
    private readonly ILogger<StorageDiagnosticController> _logger;

    public StorageDiagnosticController(
        IAmazonS3 s3Client,
        IOptions<R2Options> options,
        ILogger<StorageDiagnosticController> logger)
    {
        _s3Client = s3Client;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint de diagnóstico completo para conexão, credenciais e gravação no Cloudflare R2.
    /// </summary>
    [HttpGet("test")]
    public async Task<IActionResult> RunFullDiagnostic([FromQuery] string? objectKeyToCheck, CancellationToken cancellationToken)
    {
        var result = new DiagnosticResult
        {
            Timestamp = DateTime.UtcNow,
            BucketConfigured = _options.BucketName,
            ServiceUrlConfigured = _options.ServiceUrl,
            AccountIdPresent = !string.IsNullOrWhiteSpace(_options.AccountId),
            AccessKeyPresent = !string.IsNullOrWhiteSpace(_options.AccessKey),
            AccessKeyPrefix = !string.IsNullOrWhiteSpace(_options.AccessKey) && _options.AccessKey.Length > 4
                ? _options.AccessKey[..4] + "..."
                : "(ausente)",
            SecretKeyPresent = !string.IsNullOrWhiteSpace(_options.SecretKey),
            IsConfigured = _options.IsConfigured
        };

        if (!_options.IsConfigured)
        {
            result.Status = "FAILED_CONFIG";
            result.Message = "As variáveis de ambiente do Cloudflare R2 não estão completamente configuradas (R2__AccountId, R2__AccessKey, R2__SecretKey, R2__BucketName).";
            return Ok(result);
        }

        // 1. Teste de PutObject direto pelo backend
        var testKey = $"system/diagnostic/backend-direct-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
        var testContent = $"PRAXIS R2 Diagnostic Test at {DateTime.UtcNow:O}";
        var testBytes = Encoding.UTF8.GetBytes(testContent);

        try
        {
            using var memoryStream = new MemoryStream(testBytes);
            var putRequest = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = testKey,
                InputStream = memoryStream,
                ContentType = "text/plain"
            };

            var putResponse = await _s3Client.PutObjectAsync(putRequest, cancellationToken);
            result.DirectPutSuccess = (int)putResponse.HttpStatusCode >= 200 && (int)putResponse.HttpStatusCode < 300;
            result.DirectPutStatusCode = (int)putResponse.HttpStatusCode;
            result.DirectPutETag = putResponse.ETag;
        }
        catch (Exception ex)
        {
            result.DirectPutSuccess = false;
            result.DirectPutError = ex.Message;
            result.DirectPutExceptionType = ex.GetType().Name;
            _logger.LogError(ex, "Erro no teste PutObject direto do diagnóstico R2");
        }

        // 2. Teste de HeadObject direto no arquivo gravado
        if (result.DirectPutSuccess)
        {
            try
            {
                var headRequest = new GetObjectMetadataRequest
                {
                    BucketName = _options.BucketName,
                    Key = testKey
                };

                var headResponse = await _s3Client.GetObjectMetadataAsync(headRequest, cancellationToken);
                result.DirectHeadSuccess = true;
                result.DirectHeadContentLength = headResponse.ContentLength;
                result.DirectHeadContentType = headResponse.Headers.ContentType;
                result.DirectHeadETag = headResponse.ETag;
            }
            catch (Exception ex)
            {
                result.DirectHeadSuccess = false;
                result.DirectHeadError = ex.Message;
                _logger.LogError(ex, "Erro no teste HeadObject do diagnóstico R2");
            }

            // Limpa o arquivo de teste direto
            try
            {
                await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = testKey
                }, cancellationToken);
                result.DirectDeleteSuccess = true;
            }
            catch (Exception ex)
            {
                result.DirectDeleteSuccess = false;
                result.DirectDeleteError = ex.Message;
            }
        }

        // 3. Teste de Geração e Execução de Presigned PUT URL via HttpClient no Backend
        var presignedTestKey = $"system/diagnostic/presigned-put-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png";
        try
        {
            var presignedRequest = new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = presignedTestKey,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(15),
                ContentType = "image/png"
            };

            var presignedUrl = _s3Client.GetPreSignedURL(presignedRequest);
            result.PresignedPutUrlGenerated = true;
            result.PresignedPutUrlSample = MaskPresignedUrl(presignedUrl);

            // Simula um PUT de frontend direto na presigned URL
            using var httpClient = new HttpClient();
            using var imageContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }); // 8 bytes PNG header
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

            var httpResponse = await httpClient.PutAsync(presignedUrl, imageContent, cancellationToken);
            result.PresignedPutHttpExecuted = true;
            result.PresignedPutHttpStatusCode = (int)httpResponse.StatusCode;

            if (httpResponse.IsSuccessStatusCode)
            {
                // Verifica se o objeto realmente foi persistido no R2 via HeadObject
                var headPresigned = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = _options.BucketName,
                    Key = presignedTestKey
                }, cancellationToken);

                result.PresignedObjectExistsInR2 = true;
                result.PresignedObjectContentLength = headPresigned.ContentLength;
                result.PresignedObjectETag = headPresigned.ETag;

                // Limpa
                await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = presignedTestKey
                }, cancellationToken);
            }
            else
            {
                result.PresignedObjectExistsInR2 = false;
                var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                result.PresignedPutHttpResponseBody = errorBody;
            }
        }
        catch (Exception ex)
        {
            result.PresignedPutExecutionError = ex.Message;
            result.PresignedPutExceptionType = ex.GetType().Name;
        }

        // 4. Verificação de ObjectKey específico informado pelo usuário
        if (!string.IsNullOrWhiteSpace(objectKeyToCheck))
        {
            try
            {
                var checkRequest = new GetObjectMetadataRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKeyToCheck.Trim()
                };

                var checkResponse = await _s3Client.GetObjectMetadataAsync(checkRequest, cancellationToken);
                result.QueriedObjectResult = new ObjectCheckResult
                {
                    ObjectKey = objectKeyToCheck,
                    Exists = true,
                    ContentLength = checkResponse.ContentLength,
                    ContentType = checkResponse.Headers.ContentType,
                    ETag = checkResponse.ETag,
                    LastModified = checkResponse.LastModified
                };
            }
            catch (AmazonS3Exception ex)
            {
                result.QueriedObjectResult = new ObjectCheckResult
                {
                    ObjectKey = objectKeyToCheck,
                    Exists = false,
                    HttpStatusCode = (int)ex.StatusCode,
                    ErrorCode = ex.ErrorCode,
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                result.QueriedObjectResult = new ObjectCheckResult
                {
                    ObjectKey = objectKeyToCheck,
                    Exists = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        result.Status = (result.DirectPutSuccess && result.PresignedObjectExistsInR2) ? "HEALTHY" : "DEGRADED";
        return Ok(result);
    }

    private static string MaskPresignedUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            if (query["X-Amz-Signature"] != null)
            {
                query["X-Amz-Signature"] = "MASKED_SIGNATURE";
            }
            return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}?{query}";
        }
        catch
        {
            return "(url gerada)";
        }
    }
}

public class DiagnosticResult
{
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "UNKNOWN";
    public string? Message { get; set; }

    public string BucketConfigured { get; set; } = string.Empty;
    public string ServiceUrlConfigured { get; set; } = string.Empty;
    public bool AccountIdPresent { get; set; }
    public bool AccessKeyPresent { get; set; }
    public string AccessKeyPrefix { get; set; } = string.Empty;
    public bool SecretKeyPresent { get; set; }
    public bool IsConfigured { get; set; }

    public bool DirectPutSuccess { get; set; }
    public int? DirectPutStatusCode { get; set; }
    public string? DirectPutETag { get; set; }
    public string? DirectPutError { get; set; }
    public string? DirectPutExceptionType { get; set; }

    public bool DirectHeadSuccess { get; set; }
    public long? DirectHeadContentLength { get; set; }
    public string? DirectHeadContentType { get; set; }
    public string? DirectHeadETag { get; set; }
    public string? DirectHeadError { get; set; }

    public bool DirectDeleteSuccess { get; set; }
    public string? DirectDeleteError { get; set; }

    public bool PresignedPutUrlGenerated { get; set; }
    public string? PresignedPutUrlSample { get; set; }
    public bool PresignedPutHttpExecuted { get; set; }
    public int? PresignedPutHttpStatusCode { get; set; }
    public string? PresignedPutHttpResponseBody { get; set; }
    public bool PresignedObjectExistsInR2 { get; set; }
    public long? PresignedObjectContentLength { get; set; }
    public string? PresignedObjectETag { get; set; }
    public string? PresignedPutExecutionError { get; set; }
    public string? PresignedPutExceptionType { get; set; }

    public ObjectCheckResult? QueriedObjectResult { get; set; }
}

public class ObjectCheckResult
{
    public string ObjectKey { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public long? ContentLength { get; set; }
    public string? ContentType { get; set; }
    public string? ETag { get; set; }
    public DateTime? LastModified { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
