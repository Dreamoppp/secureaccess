namespace SecureAccess.Api.Authorization;

public static class Permissions
{
    // User management
    public const string UsersCreate = "Users.Create";
    public const string UsersRead = "Users.Read";
    public const string UsersUpdate = "Users.Update";
    public const string UsersDeactivate = "Users.Deactivate";

    // Role management
    public const string RolesRead = "Roles.Read";
    public const string RolesAssign = "Roles.Assign";

    // Permission management
    public const string PermissionsRead = "Permissions.Read";

    // Access request management
    public const string AccessRequestsCreate = "AccessRequests.Create";
    public const string AccessRequestsViewSelf = "AccessRequests.ViewSelf";
    public const string AccessRequestsViewAll = "AccessRequests.ViewAll";
    public const string AccessRequestsApprove = "AccessRequests.Approve";
    public const string AccessRequestsReject = "AccessRequests.Reject";
    public const string AccessRequestsRevoke = "AccessRequests.Revoke";

    // Audit logs
    public const string AuditLogsView = "AuditLogs.View";

    public static readonly IReadOnlyList<string> All = new[]
    {
        UsersCreate, UsersRead, UsersUpdate, UsersDeactivate,
        RolesRead, RolesAssign,
        PermissionsRead,
        AccessRequestsCreate, AccessRequestsViewSelf, AccessRequestsViewAll,
        AccessRequestsApprove, AccessRequestsReject, AccessRequestsRevoke,
        AuditLogsView
    };
}
