using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SecureAccess.Api.DTOs.AccessRequests;
using SecureAccess.Api.DTOs.Auth;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.DTOs.Roles;
using SecureAccess.Api.DTOs.Users;

namespace SecureAccess.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Direct the API to use the InMemory database provider during integration tests
        builder.UseSetting("UseInMemoryDatabase", "true");
        builder.UseEnvironment("Development");
    }
}

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUsers_WithoutToken_Returns401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentials_Returns401Unauthorized()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Username = "admin.sarah",
            Password = "WrongPassword999!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidAdminCredentials_Returns200AndValidToken()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Username = "admin.sarah",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        Assert.NotNull(content);
        Assert.True(content.Success);
        Assert.NotNull(content.Data?.Token);
        Assert.Contains("Admin", content.Data.User.Roles);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ReturnsMeProfile()
    {
        // Arrange: Login as Admin
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = "admin.sarah",
            Password = "Password123!"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        var token = loginResult!.Data!.Token;

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var meResult = await response.Content.ReadFromJsonAsync<ApiResponse<CurrentUserDto>>();
        Assert.NotNull(meResult?.Data);
        Assert.Equal("admin.sarah", meResult.Data.Username);
        Assert.Contains("Admin", meResult.Data.Roles);
        Assert.NotEmpty(meResult.Data.Permissions);
    }

    [Fact]
    public async Task Roles_GetRoles_ReturnsSeededRoles()
    {
        // Arrange: Login as Admin
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = "admin.sarah",
            Password = "Password123!"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        var token = loginResult!.Data!.Token;

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rolesResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<RoleDto>>>();
        Assert.NotNull(rolesResult?.Data);
        Assert.Contains(rolesResult.Data, r => r.RoleName == "Admin");
        Assert.Contains(rolesResult.Data, r => r.RoleName == "Approver");
        Assert.Contains(rolesResult.Data, r => r.RoleName == "Employee");
    }

    [Fact]
    public async Task AuditLogs_EmployeeWithoutAuditPermission_Returns403Forbidden()
    {
        // Arrange: Login as Employee (emp.david)
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = "emp.david",
            Password = "Password123!"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        var token = loginResult!.Data!.Token;

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/audit-logs");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act (Employee trying to view audit logs)
        var response = await _client.SendAsync(requestMessage);

        // Assert: HTTP 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRoles_ByEmployee_Returns403Forbidden()
    {
        // Arrange: Login as Employee (emp.david)
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = "emp.david",
            Password = "Password123!"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        var token = loginResult!.Data!.Token;

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, "/api/users/2/roles")
        {
            Content = JsonContent.Create(new UpdateUserRolesDto { RoleIds = new List<int> { 1 } })
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert: HTTP 403 Forbidden (Employee cannot assign roles)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateUser_ByEmployee_Returns403Forbidden()
    {
        // Arrange: Login as Employee (emp.david)
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = "emp.david",
            Password = "Password123!"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        var token = loginResult!.Data!.Token;

        var requestMessage = new HttpRequestMessage(HttpMethod.Patch, "/api/users/2/status")
        {
            Content = JsonContent.Create(new UpdateUserStatusDto { IsActive = false })
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert: HTTP 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApproveRequest_ByEmployee_Returns403Forbidden()
    {
        // Arrange: Login as Employee (emp.david)
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = "emp.david",
            Password = "Password123!"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        var token = loginResult!.Data!.Token;

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/access-requests/1/approve")
        {
            Content = JsonContent.Create(new ApproveAccessRequestDto { Comment = "Unauthorized attempt" })
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert: HTTP 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRequestById_InvalidNegativeId_Returns400BadRequest()
    {
        // Arrange: Login as Admin
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = "admin.sarah",
            Password = "Password123!"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        var token = loginResult!.Data!.Token;

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/access-requests/-5");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert: HTTP 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Contains("positive integer", body.Message);
    }

    [Fact]
    public async Task CreateRequest_MissingRequiredFields_Returns400BadRequest()
    {
        // Arrange: Login as Employee
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = "emp.david",
            Password = "Password123!"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        var token = loginResult!.Data!.Token;

        // Malformed DTO with empty ResourceName and empty Reason
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/access-requests")
        {
            Content = JsonContent.Create(new CreateAccessRequestDto
            {
                ResourceName = "",
                Reason = ""
            })
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert: HTTP 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.NotEmpty(body.Errors);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Returns401Unauthorized()
    {
        // Arrange
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "completely.invalid.jwt.token");

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert: HTTP 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_InvalidNegativeId_Returns400BadRequest()
    {
        // Arrange: Login as Admin
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = "admin.sarah",
            Password = "Password123!"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        var token = loginResult!.Data!.Token;

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/users/-10");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert: HTTP 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Contains("positive integer", body.Message);
    }

    [Fact]
    public async Task Login_EmptyCredentials_Returns400BadRequest()
    {
        // Arrange: Empty username and password
        var request = new LoginRequestDto
        {
            Username = "",
            Password = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert: HTTP 400 Bad Request with validation errors
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.NotEmpty(body.Errors);
    }
}
