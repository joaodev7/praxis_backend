namespace Praxis.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> GenerateUploadUrlAsync(string objectKey, string contentType, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task<string> GenerateDownloadUrlAsync(string objectKey, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType);
    Task<(Stream Stream, string ContentType)?> GetFileAsync(string fileName);
}
