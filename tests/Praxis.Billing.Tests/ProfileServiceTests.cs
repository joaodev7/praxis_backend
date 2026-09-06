using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Xunit;

namespace Praxis.Billing.Tests;

public class ProfileServiceTests
{
    private readonly Mock<IFileStorageService> _storageMock;
    private readonly Mock<ILogger<ProfileService>> _loggerMock;

    private static readonly byte[] ValidJpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48 };
    private static readonly byte[] ValidPngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
    private static readonly byte[] ValidWebpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20 };
    private static readonly byte[] FakeInvalidBytes = new byte[] { 0x45, 0x78, 0x65, 0x63, 0x75, 0x74, 0x61, 0x62, 0x6C, 0x65, 0x21, 0x00 };

    public ProfileServiceTests()
    {
        _storageMock = new Mock<IFileStorageService>();
        _loggerMock = new Mock<ILogger<ProfileService>>();
    }

    [Fact]
    public async Task GetProfileAsync_WhenAuthenticated_ShouldReturnCorrectUserProfile()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var user = context.Users.First();
        user.DateOfBirth = new DateOnly(1995, 8, 20);
        user.ProfilePhotoKey = $"profiles/{tenantId}/{user.Id}/avatar.webp";
        await context.SaveChangesAsync();

        _storageMock
            .Setup(s => s.GetFileUrlAsync(user.ProfilePhotoKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync($"https://r2.praxis.dev/{user.ProfilePhotoKey}");

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetProfileAsync();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.Name.Should().Be(user.Name);
        result.Email.Should().Be(user.Email);
        result.DateOfBirth.Should().Be(new DateOnly(1995, 8, 20));
        result.ProfilePhotoUrl.Should().StartWith($"https://r2.praxis.dev/{user.ProfilePhotoKey}?v=");
    }

    [Fact]
    public async Task GetProfileAsync_WhenNotAuthenticated_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        currentUserMock.Setup(c => c.UserId).Returns((Guid?)null);

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetProfileAsync());
    }

    [Fact]
    public async Task UpdateProfileAsync_WithValidData_ShouldUpdateNameAndDateOfBirth()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var user = context.Users.First();
        var initialEmail = user.Email;

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        var request = new UpdateProfileRequest("João Vitor Silva", new DateOnly(1998, 5, 20));

        // Act
        var response = await service.UpdateProfileAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Name.Should().Be("João Vitor Silva");
        response.DateOfBirth.Should().Be(new DateOnly(1998, 5, 20));
        response.Email.Should().Be(initialEmail);

        var updatedInDb = await context.Users.FindAsync(user.Id);
        updatedInDb!.Name.Should().Be("João Vitor Silva");
        updatedInDb.DateOfBirth.Should().Be(new DateOnly(1998, 5, 20));
        updatedInDb.Email.Should().Be(initialEmail); // Email remains intact
    }

    [Fact]
    public async Task UpdateProfileAsync_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        var request = new UpdateProfileRequest("   ", new DateOnly(1998, 5, 20));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateProfileAsync(request));
        ex.Message.Should().Contain("nome é obrigatório");
    }

    [Fact]
    public async Task UpdateProfileAsync_WithNameExceedingLimit_ShouldThrowArgumentException()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        var request = new UpdateProfileRequest(new string('A', 151), new DateOnly(1998, 5, 20));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateProfileAsync(request));
        ex.Message.Should().Contain("150 caracteres");
    }

    [Fact]
    public async Task UpdateProfileAsync_WithFutureDateOfBirth_ShouldThrowArgumentException()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        var request = new UpdateProfileRequest("João Vitor", futureDate);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateProfileAsync(request));
        ex.Message.Should().Contain("inválida");
    }

    [Theory]
    [InlineData("avatar.jpg", "image/jpeg", 0)] // JPEG
    [InlineData("avatar.png", "image/png", 1)]  // PNG
    [InlineData("avatar.webp", "image/webp", 2)] // WebP
    public async Task UploadPhotoAsync_WithValidImageFormats_ShouldUploadToR2AndSaveKey(string fileName, string contentType, int formatIndex)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var user = context.Users.First();
        var expectedKey = $"profiles/{tenantId}/{user.Id}/avatar.webp";

        _storageMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), expectedKey, "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedKey);

        _storageMock
            .Setup(s => s.GetFileUrlAsync(expectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync($"https://r2.praxis.dev/{expectedKey}");

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        byte[] rawBytes = formatIndex switch
        {
            0 => ValidJpegBytes,
            1 => ValidPngBytes,
            _ => ValidWebpBytes
        };

        using var stream = new MemoryStream(rawBytes);

        // Act
        var response = await service.UploadPhotoAsync(stream, fileName, contentType, rawBytes.Length);

        // Assert
        response.Should().NotBeNull();
        response.ProfilePhotoUrl.Should().StartWith($"https://r2.praxis.dev/{expectedKey}?v=");

        var updatedUser = await context.Users.FindAsync(user.Id);
        updatedUser!.ProfilePhotoKey.Should().Be(expectedKey);
        updatedUser.ProfilePhotoUrl.Should().StartWith($"https://r2.praxis.dev/{expectedKey}?v=");

        _storageMock.Verify(s => s.UploadAsync(It.IsAny<Stream>(), expectedKey, "image/webp", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithInvalidFileSignature_ShouldThrowArgumentException()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        using var stream = new MemoryStream(FakeInvalidBytes);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadPhotoAsync(stream, "script.jpg", "image/jpeg", FakeInvalidBytes.Length));

        ex.Message.Should().Contain("não possui um formato válido");
        _storageMock.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithFileExceeding5MB_ShouldThrowArgumentException()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        using var stream = new MemoryStream(new byte[10]);

        var oversizedBytes = 6 * 1024 * 1024; // 6 MB

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadPhotoAsync(stream, "foto.jpg", "image/jpeg", oversizedBytes));

        ex.Message.Should().Contain("5 MB");
    }

    [Fact]
    public async Task UploadPhotoAsync_WhenReplacingExistingPhoto_ShouldUploadNewAndKeepDBUpdated()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var user = context.Users.First();
        var oldKey = $"profiles/{tenantId}/{user.Id}/old-avatar.webp";
        user.ProfilePhotoKey = oldKey;
        user.ProfilePhotoUrl = $"https://r2.praxis.dev/{oldKey}";
        await context.SaveChangesAsync();

        var newKey = $"profiles/{tenantId}/{user.Id}/avatar.webp";

        _storageMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), newKey, "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newKey);

        _storageMock
            .Setup(s => s.GetFileUrlAsync(newKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync($"https://r2.praxis.dev/{newKey}");

        _storageMock
            .Setup(s => s.DeleteAsync(oldKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        using var stream = new MemoryStream(ValidPngBytes);

        // Act
        var response = await service.UploadPhotoAsync(stream, "new_photo.png", "image/png", ValidPngBytes.Length);

        // Assert
        response.ProfilePhotoUrl.Should().StartWith($"https://r2.praxis.dev/{newKey}?v=");

        var updatedUser = await context.Users.FindAsync(user.Id);
        updatedUser!.ProfilePhotoKey.Should().Be(newKey);

        // Old key was different, so DeleteAsync should be called on it
        _storageMock.Verify(s => s.DeleteAsync(oldKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePhotoAsync_WhenPhotoExists_ShouldRemoveFromR2AndClearDatabaseFields()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantId);
        using var conn = connection;

        var user = context.Users.First();
        var existingKey = $"profiles/{tenantId}/{user.Id}/avatar.webp";
        user.ProfilePhotoKey = existingKey;
        user.ProfilePhotoUrl = $"https://r2.praxis.dev/{existingKey}";
        await context.SaveChangesAsync();

        _storageMock
            .Setup(s => s.DeleteAsync(existingKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act
        await service.DeletePhotoAsync();

        // Assert
        var updatedUser = await context.Users.FindAsync(user.Id);
        updatedUser!.ProfilePhotoKey.Should().BeNull();
        updatedUser.ProfilePhotoUrl.Should().BeNull();

        _storageMock.Verify(s => s.DeleteAsync(existingKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePhotoAsync_WhenNoPhotoExists_ShouldBeIdempotentAndNotThrow()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var user = context.Users.First();
        user.ProfilePhotoKey = null;
        user.ProfilePhotoUrl = null;
        await context.SaveChangesAsync();

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act
        var act = () => service.DeletePhotoAsync();

        // Assert
        await act.Should().NotThrowAsync();
        _storageMock.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MultiTenantIsolation_UserCannotAccessOrModifyAnotherUsersPhoto()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext(tenantA);
        using var conn = connection;

        // User A is the authenticated user
        var userA = context.Users.First();
        userA.ProfilePhotoKey = $"profiles/{tenantA}/{userA.Id}/avatar.webp";
        userA.ProfilePhotoUrl = $"https://r2.praxis.dev/{userA.ProfilePhotoKey}";

        // Tenant B & User B
        var tenantEntityB = new Tenant
        {
            Id = tenantB,
            Name = "Tenant B",
            LegalName = "Tenant B Ltda",
            Cnpj = "99.888.777/0001-66",
            Email = "admin@tenantb.com",
            Phone = "(11) 98888-7777",
            Status = TenantStatus.Active
        };
        context.Tenants.Add(tenantEntityB);

        // User B belongs to Tenant B
        var userB = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            Name = "Nutricionista B",
            Email = "nutriB@tenantB.com",
            PasswordHash = "hash",
            Role = UserRole.Nutritionist,
            Status = UserStatus.Active,
            ProfilePhotoKey = $"profiles/{tenantB}/other-user/avatar.webp",
            ProfilePhotoUrl = "https://r2.praxis.dev/profiles/tenantB/other-user/avatar.webp"
        };
        context.Users.Add(userB);
        await context.SaveChangesAsync();

        var service = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);

        // Act 1: User A gets their own profile
        var profileA = await service.GetProfileAsync();
        profileA.Id.Should().Be(userA.Id);
        profileA.Name.Should().Be(userA.Name);
        profileA.Email.Should().Be(userA.Email);

        // Act 2: User A updates profile
        await service.UpdateProfileAsync(new UpdateProfileRequest("User A Updated", null));
        var userBInDb = await context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userB.Id);
        userBInDb.Name.Should().Be("Nutricionista B"); // userB unmodified

        // Act 3: User A uploads photo - always saves under User A's tenant and user id
        var expectedKeyA = $"profiles/{tenantA}/{userA.Id}/avatar.webp";
        _storageMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), expectedKeyA, "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedKeyA);
        _storageMock
            .Setup(s => s.GetFileUrlAsync(expectedKeyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync($"https://r2.praxis.dev/{expectedKeyA}");

        using var stream = new MemoryStream(ValidJpegBytes);
        await service.UploadPhotoAsync(stream, "photo.jpg", "image/jpeg", ValidJpegBytes.Length);

        // Verify that only User A's photo key was set
        var freshUserA = await context.Users.FindAsync(userA.Id);
        freshUserA!.ProfilePhotoKey.Should().Be(expectedKeyA);

        var freshUserB = await context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userB.Id);
        freshUserB.ProfilePhotoKey.Should().Be($"profiles/{tenantB}/other-user/avatar.webp");
    }
}
