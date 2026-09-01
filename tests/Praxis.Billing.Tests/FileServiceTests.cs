using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Praxis.Infrastructure.Storage;
using Xunit;

namespace Praxis.Billing.Tests;

public class FileServiceTests
{
    private readonly Mock<IFileStorageService> _storageMock;
    private readonly Mock<ILogger<FileService>> _loggerMock;

    public FileServiceTests()
    {
        _storageMock = new Mock<IFileStorageService>();
        _loggerMock = new Mock<ILogger<FileService>>();
    }

    [Fact]
    public async Task GenerateUploadUrlAsync_WithValidImage_ShouldReturnPresignedUrlAndSavePendingStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        _storageMock
            .Setup(s => s.GenerateUploadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://file-praxis-sandbox.r2.cloudflarestorage.com/presigned-put-url");

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var request = new GenerateUploadUrlRequest(
            FileName: "fachada_restaurante.jpg",
            ContentType: "image/jpeg",
            Size: 1024 * 500, // 500 KB
            Category: FileCategory.ClientPhoto
        );

        // Act
        var response = await service.GenerateUploadUrlAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.FileId.Should().NotBeEmpty();
        response.UploadUrl.Should().Be("https://file-praxis-sandbox.r2.cloudflarestorage.com/presigned-put-url");
        response.ObjectKey.Should().StartWith($"tenants/{tenantId}/general/photos/");
        response.ObjectKey.Should().EndWith(".jpg");
        response.ExpiresIn.Should().Be(900); // 15 min

        var savedFile = await context.Files.FindAsync(response.FileId);
        savedFile.Should().NotBeNull();
        savedFile!.Status.Should().Be(FileStatus.Pending);
        savedFile.OriginalFileName.Should().Be("fachada_restaurante.jpg");
        savedFile.ContentType.Should().Be("image/jpeg");
        savedFile.Size.Should().Be(1024 * 500);
        savedFile.Category.Should().Be(FileCategory.ClientPhoto);
        savedFile.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateUploadUrlAsync_WithValidPdf_ShouldReturnPresignedUrl()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        _storageMock
            .Setup(s => s.GenerateUploadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://file-praxis-sandbox.r2.cloudflarestorage.com/presigned-pdf-url");

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var request = new GenerateUploadUrlRequest(
            FileName: "laudo_tecnico_rdc216.pdf",
            ContentType: "application/pdf",
            Size: 1024 * 1024 * 3, // 3 MB
            Category: FileCategory.Report
        );

        // Act
        var response = await service.GenerateUploadUrlAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.ObjectKey.Should().StartWith($"tenants/{tenantId}/general/reports/");
        response.ObjectKey.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task GenerateUploadUrlAsync_WithInvalidMimeType_ShouldThrowArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var request = new GenerateUploadUrlRequest(
            FileName: "script_malicioso.exe",
            ContentType: "application/x-msdownload",
            Size: 1024,
            Category: FileCategory.Other
        );

        // Act & Assert
        var act = () => service.GenerateUploadUrlAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Tipo de arquivo não permitido*");
    }

    [Fact]
    public async Task GenerateUploadUrlAsync_WithExceededImageSize_ShouldThrowArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var request = new GenerateUploadUrlRequest(
            FileName: "foto_gigante.png",
            ContentType: "image/png",
            Size: 6 * 1024 * 1024, // 6 MB (max is 5 MB)
            Category: FileCategory.ClientPhoto
        );

        // Act & Assert
        var act = () => service.GenerateUploadUrlAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*excede o limite máximo permitido*");
    }

    [Fact]
    public async Task GenerateUploadUrlAsync_WithExceededPdfSize_ShouldThrowArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var request = new GenerateUploadUrlRequest(
            FileName: "relatorio_pesado.pdf",
            ContentType: "application/pdf",
            Size: 15 * 1024 * 1024, // 15 MB (max is 10 MB)
            Category: FileCategory.Report
        );

        // Act & Assert
        var act = () => service.GenerateUploadUrlAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*excede o limite máximo permitido*");
    }

    [Fact]
    public async Task ObjectKeyGeneration_ShouldSanitizePathTraversal()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        _storageMock
            .Setup(s => s.GenerateUploadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://r2/url");

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        var request = new GenerateUploadUrlRequest(
            FileName: "../../../etc/passwd.jpg",
            ContentType: "image/jpeg",
            Size: 1024,
            Category: FileCategory.ClientPhoto
        );

        // Act
        var response = await service.GenerateUploadUrlAsync(request);

        // Assert
        response.ObjectKey.Should().NotContain("..");
        response.ObjectKey.Should().StartWith($"tenants/{tenantId}/general/photos/");
        response.ObjectKey.Should().EndWith(".jpg");
    }

    [Fact]
    public async Task CompleteUploadAsync_WhenObjectExistsInR2_ShouldMarkAsUploaded()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OriginalFileName = "foto.jpg",
            ObjectKey = $"tenants/{tenantId}/general/photos/2026/test.jpg",
            ContentType = "image/jpeg",
            Size = 2048,
            Category = FileCategory.ClientPhoto,
            Status = FileStatus.Pending
        };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        _storageMock
            .Setup(s => s.ExistsAsync(file.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act
        var response = await service.CompleteUploadAsync(file.Id);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(FileStatus.Uploaded);
        response.UploadedAt.Should().NotBeNull();

        var updatedFile = await context.Files.FindAsync(file.Id);
        updatedFile!.Status.Should().Be(FileStatus.Uploaded);
        updatedFile.UploadedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteUploadAsync_WhenObjectDoesNotExistInR2_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OriginalFileName = "foto.jpg",
            ObjectKey = $"tenants/{tenantId}/general/photos/2026/test.jpg",
            ContentType = "image/jpeg",
            Size = 2048,
            Category = FileCategory.ClientPhoto,
            Status = FileStatus.Pending
        };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        _storageMock
            .Setup(s => s.ExistsAsync(file.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act & Assert
        var act = () => service.CompleteUploadAsync(file.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*não foi enviado para o Cloudflare R2*");
    }

    [Fact]
    public async Task GenerateDownloadUrlAsync_WhenUploaded_ShouldReturnPresignedGetUrl()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OriginalFileName = "documento.pdf",
            ObjectKey = $"tenants/{tenantId}/general/reports/2026/doc.pdf",
            ContentType = "application/pdf",
            Size = 4096,
            Category = FileCategory.Report,
            Status = FileStatus.Uploaded,
            UploadedAt = DateTime.UtcNow
        };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        _storageMock
            .Setup(s => s.GenerateDownloadUrlAsync(file.ObjectKey, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://file-praxis-sandbox.r2.cloudflarestorage.com/presigned-get-url");

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act
        var response = await service.GetDownloadUrlAsync(file.Id);

        // Assert
        response.Should().NotBeNull();
        response.FileId.Should().Be(file.Id);
        response.DownloadUrl.Should().Be("https://file-praxis-sandbox.r2.cloudflarestorage.com/presigned-get-url");
        response.FileName.Should().Be("documento.pdf");
        response.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task GenerateDownloadUrlAsync_WhenPending_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OriginalFileName = "documento.pdf",
            ObjectKey = $"tenants/{tenantId}/general/reports/2026/doc.pdf",
            ContentType = "application/pdf",
            Size = 4096,
            Category = FileCategory.Report,
            Status = FileStatus.Pending
        };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act & Assert
        var act = () => service.GetDownloadUrlAsync(file.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*upload ainda não foi finalizado*");
    }

    [Fact]
    public async Task TenantIsolation_CannotAccessOrDownloadFileFromAnotherTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantA);
        using var conn = connection;

        var tenantBEntity = new Tenant
        {
            Id = tenantB,
            Name = "Tenant B",
            LegalName = "Tenant B Ltda",
            Cnpj = "22.333.444/0001-55",
            Email = "admin@tenantb.com",
            Phone = "(11) 88888-7777",
            Status = TenantStatus.Active
        };
        context.Tenants.Add(tenantBEntity);

        // Create file belonging to Tenant B
        var fileTenantB = new StoredFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            OriginalFileName = "secreto_tenant_b.pdf",
            ObjectKey = $"tenants/{tenantB}/general/reports/2026/sec.pdf",
            ContentType = "application/pdf",
            Size = 1024,
            Category = FileCategory.Report,
            Status = FileStatus.Uploaded
        };
        context.Files.Add(fileTenantB);
        await context.SaveChangesAsync();

        // Service runs under Tenant A context
        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act & Assert
        var act = () => service.GetDownloadUrlAsync(fileTenantB.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Arquivo não encontrado*");
    }

    [Fact]
    public async Task TenantIsolation_CannotDeleteFileFromAnotherTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantA);
        using var conn = connection;

        var tenantBEntity = new Tenant
        {
            Id = tenantB,
            Name = "Tenant B",
            LegalName = "Tenant B Ltda",
            Cnpj = "22.333.444/0001-55",
            Email = "admin@tenantb.com",
            Phone = "(11) 88888-7777",
            Status = TenantStatus.Active
        };
        context.Tenants.Add(tenantBEntity);

        var fileTenantB = new StoredFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            OriginalFileName = "arquivo_b.jpg",
            ObjectKey = $"tenants/{tenantB}/general/photos/2026/b.jpg",
            ContentType = "image/jpeg",
            Size = 1024,
            Category = FileCategory.ClientPhoto,
            Status = FileStatus.Uploaded
        };
        context.Files.Add(fileTenantB);
        await context.SaveChangesAsync();

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act & Assert
        var act = () => service.DeleteFileAsync(fileTenantB.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Arquivo não encontrado*");

        _storageMock.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldCallStorageDeleteAndSoftDeleteInDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OriginalFileName = "apagar.png",
            ObjectKey = $"tenants/{tenantId}/general/photos/2026/apagar.png",
            ContentType = "image/png",
            Size = 1024,
            Category = FileCategory.ClientPhoto,
            Status = FileStatus.Uploaded
        };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        _storageMock
            .Setup(s => s.DeleteAsync(file.ObjectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new FileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act
        var result = await service.DeleteFileAsync(file.Id);

        // Assert
        result.Should().BeTrue();
        _storageMock.Verify(s => s.DeleteAsync(file.ObjectKey, It.IsAny<CancellationToken>()), Times.Once);

        // Query filter excludes soft-deleted files
        var fetchedFile = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(context.Files, f => f.Id == file.Id);
        fetchedFile.Should().BeNull();

        // But file exists with IsDeleted = true when query filters are ignored
        var softDeletedFile = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters(context.Files), f => f.Id == file.Id);
        softDeletedFile.Should().NotBeNull();
        softDeletedFile!.IsDeleted.Should().BeTrue();
        softDeletedFile.Status.Should().Be(FileStatus.Deleted);
        softDeletedFile.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void R2Options_Validation_ShouldDetectConfiguredStatus()
    {
        // Arrange & Act
        var validOptions = new R2Options
        {
            AccountId = "3c73a89fae6057f74c8b82b6fd1813d1",
            AccessKey = "sample_key",
            SecretKey = "sample_secret",
            BucketName = "file-praxis-sandbox"
        };

        var invalidOptions = new R2Options
        {
            AccountId = "",
            AccessKey = "",
            SecretKey = "",
            BucketName = "file-praxis-sandbox"
        };

        // Assert
        validOptions.IsConfigured.Should().BeTrue();
        validOptions.ServiceUrl.Should().Be("https://3c73a89fae6057f74c8b82b6fd1813d1.r2.cloudflarestorage.com");

        invalidOptions.IsConfigured.Should().BeFalse();
        invalidOptions.ServiceUrl.Should().BeEmpty();
    }
}
