using SecureAccess.Api.DTOs.Auth;

namespace SecureAccess.WinForms.Services;

/// <summary>
/// Singleton service managing the in-memory state of the currently authenticated user.
/// Plaintext tokens and user credentials are strictly kept in memory and cleared on logout.
/// </summary>
public class UserSession
{
    private static UserSession? _instance;
    public static UserSession Instance => _instance ??= new UserSession();

    public string? Token { get; private set; }
    public DateTime? TokenExpiresAt { get; private set; }
    public CurrentUserDto? CurrentUser { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token) &&
                                   TokenExpiresAt.HasValue &&
                                   TokenExpiresAt.Value > DateTime.UtcNow;

    private UserSession() { }

    public void SetSession(string token, DateTime expiresAt, CurrentUserDto user)
    {
        Token = token;
        TokenExpiresAt = expiresAt;
        CurrentUser = user;
    }

    public bool HasPermission(string permissionName)
    {
        if (CurrentUser == null) return false;
        if (IsInRole("Admin")) return true;
        return CurrentUser.Permissions.Any(p => string.Equals(p, permissionName, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsInRole(string roleName)
    {
        if (CurrentUser == null) return false;
        return CurrentUser.Roles.Any(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));
    }

    public void Clear()
    {
        Token = null;
        TokenExpiresAt = null;
        CurrentUser = null;
    }
}
