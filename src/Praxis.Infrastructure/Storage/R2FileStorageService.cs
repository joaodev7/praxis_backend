using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Praxis.Application.Interfaces;

namespace Praxis.Infrastructure.Storage;

public class R2FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly R2Options _options;
    private readonly ILogger<R2FileStorageService> _logger;

    public R2FileStorageService(
        IAmazonS3 s3Client,
        IOptions<R2Options> options,
        ILogger<R2FileStorageService> logger)
    {
        _s3Client = s3Client;
        _options = options.Value;
        _logger = logger;
    }

    public Task<string> GenerateUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("ObjectKey não pode ser vazio.", nameof(objectKey));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("ContentType não pode ser vazio.", nameof(contentType));

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiration),
            ContentType = contentType
        };

        var url = _s3Client.GetPreSignedURL(request);
        _logger.LogInformation("Presigned PUT URL gerada com sucesso para o objeto: {ObjectKey} (expira em {ExpiresInMinutes} min)",
            objectKey, expiration.TotalMinutes);

        return Task.FromResult(url);
    }

    public Task<string> GenerateDownloadUrlAsync(
        string objectKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("ObjectKey não pode ser vazio.", nameof(objectKey));

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiration)
        };

        var url = _s3Client.GetPreSignedURL(request);
        _logger.LogInformation("Presigned GET URL gerada com sucesso para o objeto: {ObjectKey} (expira em {ExpiresInMinutes} min)",
            objectKey, expiration.TotalMinutes);

        return Task.FromResult(url);
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return false;

        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey
            };

            var response = await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
            return (int)response.HttpStatusCode >= 200 && (int)response.HttpStatusCode < 300;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                                           ex.ErrorCode == "NoSuchKey" ||
                                           ex.ErrorCode == "NotFound")
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao verificar existência do objeto no R2: {ObjectKey}", objectKey);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return false;

        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);
            _logger.LogInformation("Objeto excluído com sucesso do R2: {ObjectKey}", objectKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover objeto no R2: {ObjectKey}", objectKey);
            return false;
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var uniqueKey = $"system/uploads/{DateTime.UtcNow:yyyy}/{Guid.NewGuid()}{ext}";

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = uniqueKey,
            InputStream = fileStream,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(request);
        return uniqueKey;
    }

    public async Task<(Stream Stream, string ContentType)?> GetFileAsync(string fileName)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _options.BucketName,
                Key = fileName
            };

            var response = await _s3Client.GetObjectAsync(request);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            return (memoryStream, response.Headers.ContentType ?? "application/octet-stream");
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                                           ex.ErrorCode == "NoSuchKey" ||
                                           ex.ErrorCode == "NotFound")
        {
            return null;
        }
    }
}
