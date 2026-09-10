using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Praxis.Api.Controllers;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Entities;
using Xunit;

namespace Praxis.Billing.Tests;

public class ProfileControllerTests
{
    private readonly Mock<IFileStorageService> _storageMock;
    private readonly Mock<ILogger<ProfileService>> _loggerMock;

    private static readonly byte[] ValidJpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48 };

    public ProfileControllerTests()
    {
        _storageMock = new Mock<IFileStorageService>();
        _loggerMock = new Mock<ILogger<ProfileService>>();
    }

    [Fact]
    public async Task GetProfile_ShouldReturnOkWithProfile()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var profileService = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        var controller = new ProfileController(profileService);

        // Act
        var result = await controller.GetProfile(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var profile = okResult.Value as ProfileResponse;
        profile.Should().NotBeNull();
        profile!.Email.Should().Be("admin@tenant.com");
    }

    [Fact]
    public async Task UpdateProfile_WithValidRequest_ShouldReturnOkWithUpdatedProfile()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var profileService = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        var controller = new ProfileController(profileService);

        var request = new UpdateProfileRequest("Dra. Beatriz Santos", new DateOnly(1992, 3, 15));

        // Act
        var result = await controller.UpdateProfile(request, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var profile = okResult.Value as ProfileResponse;
        profile.Should().NotBeNull();
        profile!.Name.Should().Be("Dra. Beatriz Santos");
        profile.DateOfBirth.Should().Be(new DateOnly(1992, 3, 15));
    }

    [Fact]
    public async Task UploadPhoto_WhenFileIsNull_ShouldReturnBadRequest()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var profileService = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        var controller = new ProfileController(profileService);

        // Act
        var result = await controller.UploadPhoto(null, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UploadPhoto_WithValidFile_ShouldReturnOkWithUploadResponse()
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

        var profileService = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        var controller = new ProfileController(profileService);

        var formFileMock = new Mock<IFormFile>();
        var stream = new MemoryStream(ValidJpegBytes);
        formFileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        formFileMock.Setup(f => f.FileName).Returns("foto.jpg");
        formFileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        formFileMock.Setup(f => f.Length).Returns(ValidJpegBytes.Length);

        // Act
        var result = await controller.UploadPhoto(formFileMock.Object, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var uploadResponse = okResult.Value as ProfilePhotoUploadResponse;
        uploadResponse.Should().NotBeNull();
        uploadResponse!.ProfilePhotoUrl.Should().StartWith($"https://r2.praxis.dev/{expectedKey}?v=");
    }

    [Fact]
    public async Task DeletePhoto_ShouldReturnNoContent()
    {
        // Arrange
        var (context, currentUserMock, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var conn = connection;

        var profileService = new ProfileService(context, currentUserMock.Object, _storageMock.Object, _loggerMock.Object);
        var controller = new ProfileController(profileService);

        // Act
        var result = await controller.DeletePhoto(CancellationToken.None);

        // Assert
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(204);
    }
}
