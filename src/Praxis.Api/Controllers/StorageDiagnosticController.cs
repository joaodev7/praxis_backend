using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Praxis.Infrastructure.Storage;
using System.Net.Http.Headers;
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
    /// Endpoint de diagnóstico completo para Cloudflare R2:
    /// 1. Configuração sanitizada das variáveis
    /// 2. PutObject direto via AWS SDK (DisablePayloadSigning e DisableDefaultChecksumValidation)
    /// 3. HeadObject no objeto gravado
    /// 4. Geração de Presigned PUT URL
    /// 5. Upload PUT através da Presigned URL via HttpClient
    /// 6. HeadObject no objeto enviado via Presigned URL
    /// 7. Exclusão e limpeza dos objetos de teste
    /// </summary>
    [HttpGet("test")]
    public async Task<IActionResult> RunStorageDiagnostic([FromQuery] string? objectKeyToCheck, CancellationToken cancellationToken)
    {
        var rawAccountId = _options.AccountId?.Trim() ?? string.Empty;
        var serviceUrl = _options.ServiceUrl;

        var result = new StorageDiagnosticResponse
        {
            Timestamp = DateTime.UtcNow,
            Bucket = _options.BucketName,
            ServiceUrl = serviceUrl,
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            AccountIdConfigured = !string.IsNullOrWhiteSpace(_options.AccountId),
            AccessKeyConfigured = !string.IsNullOrWhiteSpace(_options.AccessKey),
            AccessKeyPrefix = !string.IsNullOrWhiteSpace(_options.AccessKey) && _options.AccessKey.Length >= 4
                ? _options.AccessKey[..4] + "..."
                : "(ausente)",
            SecretKeyConfigured = !string.IsNullOrWhiteSpace(_options.SecretKey),
            IsFullyConfigured = _options.IsConfigured
        };

        if (!_options.IsConfigured)
        {
            result.Status = "CONFIG_INCOMPLETE";
            result.Message = "As variáveis de ambiente do Cloudflare R2 não estão completamente configuradas (R2__AccountId, R2__AccessKey, R2__SecretKey, R2__BucketName).";
            return Ok(result);
        }

        // =========================================================================
        // ETAPA 1 & 2: PutObject direto via AWS SDK + HeadObject
        // =========================================================================
        var directKey = $"system/diagnostic/direct-put-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
        var directContent = $"PRAXIS R2 Direct Put Test at {DateTime.UtcNow:O}";
        var directBytes = Encoding.UTF8.GetBytes(directContent);

        try
        {
            using var memoryStream = new MemoryStream(directBytes);
            var putRequest = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = directKey,
                InputStream = memoryStream,
                ContentType = "text/plain",
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            };

            var putResponse = await _s3Client.PutObjectAsync(putRequest, cancellationToken);
            result.AwsSdkPutSuccess = (int)putResponse.HttpStatusCode >= 200 && (int)putResponse.HttpStatusCode < 300;
            result.AwsSdkPutStatusCode = (int)putResponse.HttpStatusCode;
            result.AwsSdkPutETag = putResponse.ETag;
            result.AwsSdkPutKey = directKey;

            // HeadObject imediato para confirmar existência
            var headDirect = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.BucketName,
                Key = directKey
            }, cancellationToken);

            result.DirectHeadSuccess = true;
            result.DirectHeadContentLength = headDirect.ContentLength;
            result.DirectHeadContentType = headDirect.Headers.ContentType;
            result.DirectHeadETag = headDirect.ETag;

            // Limpeza do teste direto
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = directKey
            }, cancellationToken);
            result.DirectCleanupSuccess = true;
        }
        catch (Exception ex)
        {
            result.AwsSdkPutSuccess = false;
            result.AwsSdkPutError = ex.Message;
            result.AwsSdkPutExceptionType = ex.GetType().FullName;
            result.AwsSdkPutExceptionChain = GetExceptionChain(ex);
            _logger.LogError(ex, "Erro no teste PutObject do AWS SDK");
        }

        // =========================================================================
        // ETAPA 3 & 4: Presigned PUT URL + Execução de PUT HTTP + HeadObject
        // =========================================================================
        var presignedKey = $"system/diagnostic/presigned-put-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png";
        try
        {
            var presignedRequest = new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = presignedKey,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(15),
                ContentType = "image/png"
            };

            var presignedUrl = _s3Client.GetPreSignedURL(presignedRequest);
            result.PresignedPutUrlGenerated = true;
            result.PresignedKey = presignedKey;
            result.PresignedUrlSanitized = SanitizePresignedUrl(presignedUrl);

            // Executa PUT diretamente para a Presigned URL usando HttpClient
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            // 8 bytes padrão de cabeçalho PNG
            var samplePngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            using var imageContent = new ByteArrayContent(samplePngBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            var putHttpResponse = await httpClient.PutAsync(presignedUrl, imageContent, cancellationToken);
            result.PresignedPutExecuted = true;
            result.PresignedPutHttpStatusCode = (int)putHttpResponse.StatusCode;
            result.PresignedPutSuccess = putHttpResponse.IsSuccessStatusCode;

            if (putHttpResponse.IsSuccessStatusCode)
            {
                // HeadObject para confirmar persistência via presigned URL
                var headPresigned = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = _options.BucketName,
                    Key = presignedKey
                }, cancellationToken);

                result.PresignedHeadSuccess = true;
                result.PresignedHeadContentLength = headPresigned.ContentLength;
                result.PresignedHeadContentType = headPresigned.Headers.ContentType;
                result.PresignedHeadETag = headPresigned.ETag;

                // Limpeza do teste de presigned
                await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = presignedKey
                }, cancellationToken);
                result.PresignedCleanupSuccess = true;
            }
            else
            {
                var errorBody = await putHttpResponse.Content.ReadAsStringAsync(cancellationToken);
                result.PresignedPutResponseBody = errorBody;
            }
        }
        catch (Exception ex)
        {
            result.PresignedPutSuccess = false;
            result.PresignedPutError = ex.Message;
            result.PresignedPutExceptionType = ex.GetType().FullName;
            result.PresignedPutExceptionChain = GetExceptionChain(ex);
            _logger.LogError(ex, "Erro no teste Presigned PUT");
        }

        // =========================================================================
        // ETAPA 5: Consulta de ObjectKey específico (se solicitado)
        // =========================================================================
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
                result.QueriedObjectResult = new QueriedObjectInfo
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
                result.QueriedObjectResult = new QueriedObjectInfo
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
                result.QueriedObjectResult = new QueriedObjectInfo
                {
                    ObjectKey = objectKeyToCheck,
                    Exists = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        result.Status = (result.AwsSdkPutSuccess && result.DirectHeadSuccess && result.PresignedPutSuccess && result.PresignedHeadSuccess)
            ? "ALL_TESTS_PASSED_HEALTHY"
            : "DEGRADED";

        return Ok(result);
    }

    private static string SanitizePresignedUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            if (query["X-Amz-Signature"] != null)
            {
                query["X-Amz-Signature"] = "MASKED_SIGNATURE";
            }
            if (query["X-Amz-Credential"] != null)
            {
                var cred = query["X-Amz-Credential"]!;
                var parts = cred.Split('/');
                if (parts.Length > 0 && parts[0].Length >= 4)
                {
                    parts[0] = parts[0][..4] + "...";
                    query["X-Amz-Credential"] = string.Join('/', parts);
                }
            }
            return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}?{query}";
        }
        catch
        {
            return "(url pré-assinada sanitizada)";
        }
    }

    private static List<ExceptionDetail> GetExceptionChain(Exception? ex)
    {
        var chain = new List<ExceptionDetail>();
        while (ex != null)
        {
            chain.Add(new ExceptionDetail
            {
                Type = ex.GetType().FullName ?? ex.GetType().Name,
                Message = ex.Message,
                Source = ex.Source
            });
            ex = ex.InnerException;
        }
        return chain;
    }
}

public class StorageDiagnosticResponse
{
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "UNKNOWN";
    public string? Message { get; set; }

    public string Bucket { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = string.Empty;
    public string AuthenticationRegion { get; set; } = "auto";
    public bool ForcePathStyle { get; set; } = true;

    public bool AccountIdConfigured { get; set; }
    public bool AccessKeyConfigured { get; set; }
    public string AccessKeyPrefix { get; set; } = string.Empty;
    public bool SecretKeyConfigured { get; set; }
    public bool IsFullyConfigured { get; set; }

    // Teste 1: Direct AWS SDK PutObject & HeadObject
    public bool AwsSdkPutSuccess { get; set; }
    public int? AwsSdkPutStatusCode { get; set; }
    public string? AwsSdkPutETag { get; set; }
    public string? AwsSdkPutKey { get; set; }
    public string? AwsSdkPutError { get; set; }
    public string? AwsSdkPutExceptionType { get; set; }
    public List<ExceptionDetail>? AwsSdkPutExceptionChain { get; set; }

    public bool DirectHeadSuccess { get; set; }
    public long? DirectHeadContentLength { get; set; }
    public string? DirectHeadContentType { get; set; }
    public string? DirectHeadETag { get; set; }
    public bool DirectCleanupSuccess { get; set; }

    // Teste 2: Presigned PUT URL & Direct HTTP PUT & HeadObject
    public bool PresignedPutUrlGenerated { get; set; }
    public string? PresignedKey { get; set; }
    public string? PresignedUrlSanitized { get; set; }
    public bool PresignedPutExecuted { get; set; }
    public int? PresignedPutHttpStatusCode { get; set; }
    public bool PresignedPutSuccess { get; set; }
    public string? PresignedPutResponseBody { get; set; }
    public string? PresignedPutError { get; set; }
    public string? PresignedPutExceptionType { get; set; }
    public List<ExceptionDetail>? PresignedPutExceptionChain { get; set; }

    public bool PresignedHeadSuccess { get; set; }
    public long? PresignedHeadContentLength { get; set; }
    public string? PresignedHeadContentType { get; set; }
    public string? PresignedHeadETag { get; set; }
    public bool PresignedCleanupSuccess { get; set; }

    // Consulta específica de objeto
    public QueriedObjectInfo? QueriedObjectResult { get; set; }
}

public class QueriedObjectInfo
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

public class ExceptionDetail
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
}
