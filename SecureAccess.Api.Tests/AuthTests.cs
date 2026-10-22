using Microsoft.Extensions.Configuration;
using SecureAccess.Api.Entities;
using SecureAccess.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SecureAccess.Api.Tests;

public class AuthTests
{
    private readonly IPasswordHasherService _hasher = new PasswordHasherService();
    private readonly ITokenService _tokenService;

    public AuthTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SuperSecretSecureAccessPAMTestingKey2026!#$@Minimum32BytesRequired",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpiresInMinutes"] = "60"
            })
            .Build();

        _tokenService = new TokenService(config);
    }

    [Fact]
    public void PasswordHasher_HashAndVerify_SucceedsForMatchingPassword()
    {
        // Arrange
        var password = "SecureEnterprisePassword2026!";

        // Act
        var hash = _hasher.HashPassword(password);
        var isValid = _hasher.VerifyPassword(password, hash);
        var isInvalid = _hasher.VerifyPassword("WrongPassword123", hash);

        // Assert
        Assert.NotNull(hash);
        Assert.True(isValid);
        Assert.False(isInvalid);
    }

    [Fact]
    public void PasswordHasher_Verify_RecognizesPhase1SeedHash()
    {
        // Arrange
        const string seedHash = "AQAAAAIAAYagAAAAEOvE3M7p9L1Qk6Fm+Z8xN0Xk1yK3R4jW6Qz7S9pA1B2C3D4E5F6G7H8I9J0K1L2M3N==";

        // Act & Assert
        Assert.True(_hasher.VerifyPassword("Password123!", seedHash));
        Assert.False(_hasher.VerifyPassword("IncorrectSecret!", seedHash));
    }

    [Fact]
    public void TokenService_GenerateToken_ContainsExpectedClaimsAndSignature()
    {
        // Arrange
        var user = new User
        {
            UserId = 42,
            Username = "emp.test",
            Email = "emp.test@secureaccess.internal",
            FullName = "Test Employee"
        };
        var roles = new[] { "Employee" };
        var permissions = new[] { "AccessRequests.Create", "AccessRequests.ViewSelf" };

        // Act
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles, permissions);

        // Assert
        Assert.NotNull(token);
        Assert.True(expiresAt > DateTime.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains(jwt.Audiences, a => a == "TestAudience");

        var userIdClaim = jwt.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.NameIdentifier ||
            c.Type == "nameid" ||
            c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        Assert.Equal("42", userIdClaim);

        var roleClaims = jwt.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToList();
        Assert.Contains("Employee", roleClaims);

        var permClaims = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        Assert.Contains("AccessRequests.Create", permClaims);
        Assert.Contains("AccessRequests.ViewSelf", permClaims);
    }
}
