using System.Net;
using SecureAccess.Api.DTOs.Auth;
using SecureAccess.WinForms.Services;

namespace SecureAccess.Api.Tests;

public class WinFormsClientTests
{
    [Fact]
    public void UserSession_SetAndEvaluateRoles_CorrectlyResolvesAdminSuperuser()
    {
        // Arrange
        var session = UserSession.Instance;
        session.Clear();

        var adminUser = new CurrentUserDto
        {
            UserId = 1,
            Username = "admin.sarah",
            FullName = "Sarah Jenkins",
            Email = "sarah.jenkins@test.internal",
            IsActive = true,
            Roles = new List<string> { "Admin" },
            Permissions = new List<string> { "Users.Read" }
        };

        // Act
        session.SetSession("fake-jwt-token", DateTime.UtcNow.AddHours(2), adminUser);

        // Assert
        Assert.True(session.IsAuthenticated);
        Assert.True(session.IsInRole("Admin"));
        Assert.False(session.IsInRole("Approver"));

        // Admin should possess superuser access even for unlisted permission
        Assert.True(session.HasPermission("Arbitrary.Superuser.Action"));
    }

    [Fact]
    public void UserSession_EmployeeRole_RespectsStrictPermissionBoundary()
    {
        // Arrange
        var session = UserSession.Instance;
        session.Clear();

        var empUser = new CurrentUserDto
        {
            UserId = 10,
            Username = "emp.david",
            FullName = "David Chen",
            Email = "david.chen@test.internal",
            IsActive = true,
            Roles = new List<string> { "Employee" },
            Permissions = new List<string> { "AccessRequests.Create", "AccessRequests.ViewSelf" }
        };

        // Act
        session.SetSession("fake-jwt-token", DateTime.UtcNow.AddHours(2), empUser);

        // Assert
        Assert.True(session.IsAuthenticated);
        Assert.True(session.HasPermission("AccessRequests.Create"));
        Assert.True(session.HasPermission("AccessRequests.ViewSelf"));

        // Employee must NOT have approval or audit permissions
        Assert.False(session.HasPermission("AccessRequests.Approve"));
        Assert.False(session.HasPermission("AuditLogs.View"));
        Assert.False(session.HasPermission("Users.Create"));
    }

    [Fact]
    public void UserSession_Clear_CompletelyWipesAuthenticationState()
    {
        // Arrange
        var session = UserSession.Instance;
        session.SetSession("active-token", DateTime.UtcNow.AddHours(1), new CurrentUserDto { Username = "temp" });
        Assert.True(session.IsAuthenticated);

        // Act
        session.Clear();

        // Assert
        Assert.False(session.IsAuthenticated);
        Assert.Null(session.Token);
        Assert.Null(session.CurrentUser);
    }

    [Fact]
    public void ApiException_EncapsulatesHttpStatusAndErrorDetails()
    {
        // Arrange
        var errors = new List<string> { "Field 'Reason' is required." };
        var ex = new ApiException(HttpStatusCode.BadRequest, "Validation failure", errors);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("Validation failure", ex.Message);
        Assert.Single(ex.Errors);
    }
}
