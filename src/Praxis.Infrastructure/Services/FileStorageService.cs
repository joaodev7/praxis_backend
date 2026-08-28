using Praxis.Application.Interfaces;

namespace Praxis.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly string _storagePath;

    public FileStorageService()
    {
        _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(_storagePath, uniqueFileName);

        using var destinationStream = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(destinationStream);

        return $"/uploads/{uniqueFileName}";
    }

    public Task<(Stream Stream, string ContentType)?> GetFileAsync(string fileName)
    {
        var filePath = Path.Combine(_storagePath, Path.GetFileName(fileName));
        if (!File.Exists(filePath))
            return Task.FromResult<(Stream Stream, string ContentType)?>(null);

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var ext = Path.GetExtension(fileName).ToLower();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };

        return Task.FromResult<(Stream Stream, string ContentType)?>((stream, contentType));
    }
}
