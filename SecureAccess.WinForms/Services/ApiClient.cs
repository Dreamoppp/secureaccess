using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SecureAccess.Api.DTOs.AccessRequests;
using SecureAccess.Api.DTOs.AuditLogs;
using SecureAccess.Api.DTOs.Auth;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.DTOs.Permissions;
using SecureAccess.Api.DTOs.Roles;
using SecureAccess.Api.DTOs.Users;

namespace SecureAccess.WinForms.Services;

public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public List<string> Errors { get; }

    public ApiException(HttpStatusCode statusCode, string message, List<string>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? new List<string>();
    }
}

public class ApiClient
{
    private static ApiClient? _instance;
    public static ApiClient Instance => _instance ??= new ApiClient();

    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string BaseUrl { get; set; } = "http://localhost:5154";

    public event Action? SessionExpired;

    private ApiClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true // Allow local dev SSL certificates
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint)
    {
        var url = $"{BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
        var request = new HttpRequestMessage(method, url);

        if (!string.IsNullOrWhiteSpace(UserSession.Instance.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", UserSession.Instance.Token);
        }

        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable,
                $"Could not connect to SecureAccess API at {BaseUrl}. Verify that the API backend is running.",
                new List<string> { ex.Message });
        }
        catch (TaskCanceledException)
        {
            throw new ApiException(HttpStatusCode.RequestTimeout,
                "The request timed out. The server took too long to respond.");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            UserSession.Instance.Clear();
            SessionExpired?.Invoke();
            throw new ApiException(HttpStatusCode.Unauthorized, "Session expired or invalid credentials. Please log in again.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new ApiException(HttpStatusCode.Forbidden, "Access Denied: You do not possess the required permission for this operation.");
        }

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            string errorMessage = $"Request failed with status code {(int)response.StatusCode} ({response.StatusCode}).";
            var errors = new List<string>();

            try
            {
                var errorObj = JsonSerializer.Deserialize<ApiResponse<object>>(content, JsonOptions);
                if (errorObj != null)
                {
                    errorMessage = !string.IsNullOrWhiteSpace(errorObj.Message) ? errorObj.Message : errorMessage;
                    errors = errorObj.Errors;
                }
            }
            catch
            {
                // Fallback to raw content if not in ApiResponse JSON schema
                if (!string.IsNullOrWhiteSpace(content))
                {
                    errorMessage = content;
                }
            }

            throw new ApiException(response.StatusCode, errorMessage, errors);
        }

        var result = JsonSerializer.Deserialize<ApiResponse<T>>(content, JsonOptions);
        if (result == null || !result.Success || result.Data == null)
        {
            throw new ApiException(response.StatusCode, result?.Message ?? "Failed to deserialize server response.", result?.Errors);
        }

        return result.Data;
    }

    // =========================================================================
    // Authentication Endpoints
    // =========================================================================

    public async Task<LoginResponseDto> LoginAsync(string username, string password)
    {
        var request = CreateRequest(HttpMethod.Post, "api/auth/login");
        request.Content = JsonContent.Create(new LoginRequestDto
        {
            Username = username,
            Password = password
        });

        return await SendAsync<LoginResponseDto>(request);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "api/auth/me");
        return await SendAsync<CurrentUserDto>(request);
    }

    // =========================================================================
    // User Management Endpoints
    // =========================================================================

    public async Task<PagedResult<UserDto>> GetUsersAsync(
        int pageNumber = 1,
        int pageSize = 20,
        bool? isActive = null,
        string? search = null)
    {
        var queryParams = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (isActive.HasValue) queryParams.Add($"isActive={isActive.Value}");
        if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");

        var endpoint = $"api/users?{string.Join("&", queryParams)}";
        var request = CreateRequest(HttpMethod.Get, endpoint);
        return await SendAsync<PagedResult<UserDto>>(request);
    }

    public async Task<UserDto> GetUserByIdAsync(int id)
    {
        var request = CreateRequest(HttpMethod.Get, $"api/users/{id}");
        return await SendAsync<UserDto>(request);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
        var request = CreateRequest(HttpMethod.Post, "api/users");
        request.Content = JsonContent.Create(dto);
        return await SendAsync<UserDto>(request);
    }

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var request = CreateRequest(HttpMethod.Put, $"api/users/{id}");
        request.Content = JsonContent.Create(dto);
        return await SendAsync<UserDto>(request);
    }

    public async Task<UserDto> UpdateUserStatusAsync(int id, bool isActive)
    {
        var request = CreateRequest(HttpMethod.Patch, $"api/users/{id}/status");
        request.Content = JsonContent.Create(new UpdateUserStatusDto { IsActive = isActive });
        return await SendAsync<UserDto>(request);
    }

    public async Task<UserDto> UpdateUserRolesAsync(int id, List<int> roleIds)
    {
        var request = CreateRequest(HttpMethod.Put, $"api/users/{id}/roles");
        request.Content = JsonContent.Create(new UpdateUserRolesDto { RoleIds = roleIds });
        return await SendAsync<UserDto>(request);
    }

    // =========================================================================
    // Roles & Permissions Endpoints
    // =========================================================================

    public async Task<List<RoleDto>> GetRolesAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "api/roles");
        return await SendAsync<List<RoleDto>>(request);
    }

    public async Task<List<PermissionDto>> GetPermissionsAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "api/permissions");
        return await SendAsync<List<PermissionDto>>(request);
    }

    // =========================================================================
    // Access Requests Endpoints
    // =========================================================================

    public async Task<PagedResult<AccessRequestDto>> GetAccessRequestsAsync(
        string? status = null,
        int? requesterId = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var queryParams = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(status) && status != "All") queryParams.Add($"status={Uri.EscapeDataString(status)}");
        if (requesterId.HasValue) queryParams.Add($"requesterId={requesterId.Value}");

        var endpoint = $"api/access-requests?{string.Join("&", queryParams)}";
        var request = CreateRequest(HttpMethod.Get, endpoint);
        return await SendAsync<PagedResult<AccessRequestDto>>(request);
    }

    public async Task<AccessRequestDto> GetAccessRequestByIdAsync(int id)
    {
        var request = CreateRequest(HttpMethod.Get, $"api/access-requests/{id}");
        return await SendAsync<AccessRequestDto>(request);
    }

    public async Task<AccessRequestDto> CreateAccessRequestAsync(CreateAccessRequestDto dto)
    {
        var request = CreateRequest(HttpMethod.Post, "api/access-requests");
        request.Content = JsonContent.Create(dto);
        return await SendAsync<AccessRequestDto>(request);
    }

    public async Task<AccessRequestDto> ApproveAccessRequestAsync(int id, ApproveAccessRequestDto dto)
    {
        var request = CreateRequest(HttpMethod.Post, $"api/access-requests/{id}/approve");
        request.Content = JsonContent.Create(dto);
        return await SendAsync<AccessRequestDto>(request);
    }

    public async Task<AccessRequestDto> RejectAccessRequestAsync(int id, RejectAccessRequestDto dto)
    {
        var request = CreateRequest(HttpMethod.Post, $"api/access-requests/{id}/reject");
        request.Content = JsonContent.Create(dto);
        return await SendAsync<AccessRequestDto>(request);
    }

    public async Task<AccessRequestDto> RevokeAccessRequestAsync(int id, RevokeAccessRequestDto dto)
    {
        var request = CreateRequest(HttpMethod.Post, $"api/access-requests/{id}/revoke");
        request.Content = JsonContent.Create(dto);
        return await SendAsync<AccessRequestDto>(request);
    }

    // =========================================================================
    // Audit Logs Endpoints
    // =========================================================================

    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? action = null,
        string? resourceType = null)
    {
        var queryParams = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(action)) queryParams.Add($"action={Uri.EscapeDataString(action)}");
        if (!string.IsNullOrWhiteSpace(resourceType)) queryParams.Add($"resourceType={Uri.EscapeDataString(resourceType)}");

        var endpoint = $"api/audit-logs?{string.Join("&", queryParams)}";
        var request = CreateRequest(HttpMethod.Get, endpoint);
        return await SendAsync<PagedResult<AuditLogDto>>(request);
    }
}
