using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Praxis.Application.DTOs;
using Praxis.Application.Interfaces;
using Praxis.Application.Services;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using Praxis.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Praxis.Billing.Tests;

public class PasswordResetTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IDisposable _connection;
    private readonly Mock<IEmailService> _emailMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IJwtTokenService> _jwtMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly AuthService _sut;
    private readonly Guid _tenantId;

    public PasswordResetTests()
    {
        _tenantId = Guid.NewGuid();
        var (context, _, connection) = TestDbContextFactory.CreateInMemoryDbContext(_tenantId);
        _context = context;
        _connection = connection;

        _emailMock = new Mock<IEmailService>();
        _configMock = new Mock<IConfiguration>();
        _jwtMock = new Mock<IJwtTokenService>();
        _currentUserMock = new Mock<ICurrentUserService>();

        _configMock.Setup(c => c["APP_BASE_URL"]).Returns("http://localhost:5173");

        _sut = new AuthService(
            _context,
            _jwtMock.Object,
            _currentUserMock.Object,
            _emailMock.Object,
            _configMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserExists_ShouldGenerateTokenAndSendEmail()
    {
        // Arrange
        var user = new User
        {
            TenantId = _tenantId,
            Name = "Nutricionista Teste",
            Email = "nutri@teste.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SenhaAntiga123"),
            Role = UserRole.Nutritionist,
            Status = UserStatus.Active
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        string? capturedResetLink = null;
        _emailMock
            .Setup(e => e.SendPasswordResetEmailAsync(
                user.Email,
                user.Name,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, link, _) => capturedResetLink = link)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest("nutri@teste.com"));

        // Assert
        result.Message.Should().Contain("Se o e-mail informado estiver cadastrado");

        var updatedUser = await _context.Users.FirstAsync(u => u.Id == user.Id);
        updatedUser.PasswordResetTokenHash.Should().NotBeNullOrWhiteSpace();
        updatedUser.PasswordResetTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);

        capturedResetLink.Should().NotBeNull();
        capturedResetLink.Should().StartWith("http://localhost:5173/redefinir-senha?token=");

        _emailMock.Verify(e => e.SendPasswordResetEmailAsync(
            user.Email,
            user.Name,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserDoesNotExist_ShouldReturnGenericMessageWithoutSendingEmail()
    {
        // Act
        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest("desconhecido@teste.com"));

        // Assert
        result.Message.Should().Contain("Se o e-mail informado estiver cadastrado");
        _emailMock.Verify(e => e.SendPasswordResetEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidToken_ShouldUpdatePasswordHashAndClearToken()
    {
        // Arrange
        var rawToken = "mysecretvalidtoken1234567890abcdef";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        var user = new User
        {
            TenantId = _tenantId,
            Name = "Nutricionista Ativo",
            Email = "ativo@teste.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SenhaAntiga123"),
            PasswordResetTokenHash = tokenHash,
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            Role = UserRole.Nutritionist,
            Status = UserStatus.Active
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var newPassword = "NovaSenhaForte@2026";

        // Act
        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest(rawToken, newPassword));

        // Assert
        result.Message.Should().Contain("sucesso");

        var updatedUser = await _context.Users.FirstAsync(u => u.Id == user.Id);
        updatedUser.PasswordResetTokenHash.Should().BeNull();
        updatedUser.PasswordResetTokenExpiresAt.Should().BeNull();
        BCrypt.Net.BCrypt.Verify(newPassword, updatedUser.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_WithExpiredToken_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var rawToken = "expiredtoken123";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        var user = new User
        {
            TenantId = _tenantId,
            Name = "Nutricionista Expirado",
            Email = "expirado@teste.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SenhaAntiga123"),
            PasswordResetTokenHash = tokenHash,
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expirado
            Role = UserRole.Nutritionist,
            Status = UserStatus.Active
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _sut.ResetPasswordAsync(new ResetPasswordRequest(rawToken, "NovaSenha123"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expirou*");
    }

    [Fact]
    public async Task ResetPasswordAsync_WithShortPassword_ShouldThrowArgumentException()
    {
        // Act
        var act = async () => await _sut.ResetPasswordAsync(new ResetPasswordRequest("anytoken", "123"));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*8 caracteres*");
    }
}
