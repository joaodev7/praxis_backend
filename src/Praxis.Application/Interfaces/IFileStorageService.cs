namespace Praxis.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType);
    Task<(Stream Stream, string ContentType)?> GetFileAsync(string fileName);
}
