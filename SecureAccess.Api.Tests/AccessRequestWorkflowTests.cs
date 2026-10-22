using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SecureAccess.Api.Data;
using SecureAccess.Api.DTOs.AccessRequests;
using SecureAccess.Api.Entities;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Tests;

public class AccessRequestWorkflowTests
{
    private SecureAccessDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SecureAccessDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new SecureAccessDbContext(options);

        // Seed basic users for testing
        if (!context.Users.Any())
        {
            var emp = new User
            {
                UserId = 10,
                Username = "emp.david",
                FullName = "David Chen",
                Email = "david.chen@test.internal",
                PasswordHash = "dummy",
                IsActive = true
            };
            var approver = new User
            {
                UserId = 20,
                Username = "approver.marcus",
                FullName = "Marcus Vance",
                Email = "marcus.vance@test.internal",
                PasswordHash = "dummy",
                IsActive = true
            };

            context.Users.AddRange(emp, approver);
            context.SaveChanges();
        }

        return context;
    }

    [Fact]
    public async Task CreateRequest_SetsStatusPendingAndLogsAudit()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(CreateRequest_SetsStatusPendingAndLogsAudit));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var dto = new CreateAccessRequestDto
        {
            ResourceName = "SQL-PROD-FINANCE-01",
            Reason = "Testing privileged schema modification patch."
        };

        // Act
        var result = await requestService.CreateRequestAsync(10, dto, "10.0.0.1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(10, result.RequesterUserId);
        Assert.Equal("SQL-PROD-FINANCE-01", result.ResourceName);

        // Verify audit log creation
        var auditLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ACCESS_REQUEST_CREATED");
        Assert.NotNull(auditLog);
        Assert.Equal(10, auditLog.UserId);
        Assert.Equal(result.AccessRequestId.ToString(), auditLog.ResourceId);
    }

    [Fact]
    public async Task ApproveRequest_ByAuthorizedApprover_SetsApprovedAndWindow()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(ApproveRequest_ByAuthorizedApprover_SetsApprovedAndWindow));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var created = await requestService.CreateRequestAsync(10, new CreateAccessRequestDto
        {
            ResourceName = "REDIS-CACHE-01",
            Reason = "Cache invalidation test"
        }, "10.0.0.1");

        var approveDto = new ApproveAccessRequestDto
        {
            StartAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            Comment = "Approved for testing."
        };

        // Act
        var approved = await requestService.ApproveRequestAsync(created.AccessRequestId, 20, approveDto, "10.0.0.2");

        // Assert
        Assert.Equal("Approved", approved.Status);
        Assert.Equal(20, approved.ApproverUserId);
        Assert.NotNull(approved.DecisionAt);
        Assert.Equal("Approved for testing.", approved.DecisionComment);
        Assert.True(approved.IsActiveGrant);

        // Verify audit log
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ACCESS_REQUEST_APPROVED");
        Assert.NotNull(audit);
        Assert.Equal(20, audit.UserId);
    }

    [Fact]
    public async Task ApproveRequest_SelfApprovalAttempt_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(ApproveRequest_SelfApprovalAttempt_ThrowsInvalidOperationException));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        // Approver #20 creates a request for themselves
        var created = await requestService.CreateRequestAsync(20, new CreateAccessRequestDto
        {
            ResourceName = "AWS-KMS-KEYS",
            Reason = "Attempting self-approved elevation"
        }, "10.0.0.2");

        var approveDto = new ApproveAccessRequestDto { Comment = "Approving myself" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            requestService.ApproveRequestAsync(created.AccessRequestId, 20, approveDto, "10.0.0.2"));

        Assert.Contains("Self-Approval", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveRequest_DuplicateApprovalAttempt_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(ApproveRequest_DuplicateApprovalAttempt_ThrowsInvalidOperationException));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var created = await requestService.CreateRequestAsync(10, new CreateAccessRequestDto
        {
            ResourceName = "K8S-CLUSTER",
            Reason = "Node maintenance"
        }, "10.0.0.1");

        await requestService.ApproveRequestAsync(created.AccessRequestId, 20, new ApproveAccessRequestDto { Comment = "First approval" }, "10.0.0.2");

        // Act & Assert (Attempt second approval on already approved request)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            requestService.ApproveRequestAsync(created.AccessRequestId, 20, new ApproveAccessRequestDto { Comment = "Duplicate approval" }, "10.0.0.2"));

        Assert.Contains("Only 'Pending' requests can be approved", ex.Message);
    }

    [Fact]
    public async Task RejectRequest_SetsRejectedStatusAndReason()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(RejectRequest_SetsRejectedStatusAndReason));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var created = await requestService.CreateRequestAsync(10, new CreateAccessRequestDto
        {
            ResourceName = "SWIFT-TERMINAL",
            Reason = "Test financial query"
        }, "10.0.0.1");

        // Act
        var rejected = await requestService.RejectRequestAsync(created.AccessRequestId, 20, new RejectAccessRequestDto
        {
            Comment = "Denied: Policy SEC-FIN-01 non-compliance."
        }, "10.0.0.2");

        // Assert
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal(20, rejected.ApproverUserId);
        Assert.NotNull(rejected.DecisionAt);
        Assert.Equal("Denied: Policy SEC-FIN-01 non-compliance.", rejected.DecisionComment);
        Assert.False(rejected.IsActiveGrant);

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ACCESS_REQUEST_REJECTED");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task RevokeRequest_ApprovedRequest_ChangesStatusToRevokedAndTruncatesWindow()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(RevokeRequest_ApprovedRequest_ChangesStatusToRevokedAndTruncatesWindow));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var created = await requestService.CreateRequestAsync(10, new CreateAccessRequestDto
        {
            ResourceName = "PROD-SSH-BASTION",
            Reason = "Emergency patch"
        }, "10.0.0.1");

        await requestService.ApproveRequestAsync(created.AccessRequestId, 20, new ApproveAccessRequestDto
        {
            StartAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(4),
            Comment = "Approved"
        }, "10.0.0.2");

        // Act
        var revoked = await requestService.RevokeRequestAsync(created.AccessRequestId, 20, new RevokeAccessRequestDto
        {
            Comment = "Maintenance finished early."
        }, "10.0.0.2");

        // Assert
        Assert.Equal("Revoked", revoked.Status);
        Assert.False(revoked.IsActiveGrant);
        Assert.Contains("Revocation: Maintenance finished early.", revoked.DecisionComment);

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ACCESS_REQUEST_REVOKED");
        Assert.NotNull(audit);
    }

    [Fact]
    public void IsActiveGrant_EvaluatesCorrectlyForExpirationAndStatus()
    {
        // 1. Pending is not active
        var pending = new AccessRequest { Status = "Pending" };
        Assert.False(pending.IsActiveGrant());

        // 2. Approved in future window is not active yet
        var future = new AccessRequest
        {
            Status = "Approved",
            StartAt = DateTime.UtcNow.AddHours(1),
            ExpiresAt = DateTime.UtcNow.AddHours(3)
        };
        Assert.False(future.IsActiveGrant());

        // 3. Approved and currently active
        var active = new AccessRequest
        {
            Status = "Approved",
            StartAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        Assert.True(active.IsActiveGrant());

        // 4. Approved but expired
        var expired = new AccessRequest
        {
            Status = "Approved",
            StartAt = DateTime.UtcNow.AddHours(-3),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };
        Assert.False(expired.IsActiveGrant());

        // 5. Revoked is never active
        var revoked = new AccessRequest
        {
            Status = "Revoked",
            StartAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        Assert.False(revoked.IsActiveGrant());
    }

    [Fact]
    public async Task CreateRequest_StartAfterExpiry_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(CreateRequest_StartAfterExpiry_ThrowsArgumentException));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var dto = new CreateAccessRequestDto
        {
            ResourceName = "SQL-PROD-01",
            Reason = "Invalid time test",
            StartAt = DateTime.UtcNow.AddHours(4),
            ExpiresAt = DateTime.UtcNow.AddHours(2) // Invalid: Start > Expiry
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            requestService.CreateRequestAsync(10, dto, "10.0.0.1"));
    }

    [Fact]
    public async Task RejectRequest_AlreadyApproved_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(RejectRequest_AlreadyApproved_ThrowsInvalidOperationException));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var created = await requestService.CreateRequestAsync(10, new CreateAccessRequestDto
        {
            ResourceName = "K8S-CLUSTER",
            Reason = "Test patch"
        }, "10.0.0.1");

        await requestService.ApproveRequestAsync(created.AccessRequestId, 20, new ApproveAccessRequestDto(), "10.0.0.2");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            requestService.RejectRequestAsync(created.AccessRequestId, 20, new RejectAccessRequestDto { Comment = "Late reject" }, "10.0.0.2"));

        Assert.Contains("Only 'Pending' requests can be rejected", ex.Message);
    }

    [Fact]
    public async Task RevokeRequest_PendingRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(RevokeRequest_PendingRequest_ThrowsInvalidOperationException));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var created = await requestService.CreateRequestAsync(10, new CreateAccessRequestDto
        {
            ResourceName = "PROD-DB",
            Reason = "Test query"
        }, "10.0.0.1");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            requestService.RevokeRequestAsync(created.AccessRequestId, 20, new RevokeAccessRequestDto { Comment = "Premature revoke" }, "10.0.0.2"));

        Assert.Contains("Only 'Approved' requests can be revoked", ex.Message);
    }

    [Fact]
    public async Task GetRequestById_EmployeeViewingOthersRequestWithoutPermission_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(GetRequestById_EmployeeViewingOthersRequestWithoutPermission_ThrowsUnauthorizedAccessException));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        // Created by User #10
        var created = await requestService.CreateRequestAsync(10, new CreateAccessRequestDto
        {
            ResourceName = "CONFIDENTIAL-VAULT",
            Reason = "Private work"
        }, "10.0.0.1");

        // Act & Assert: Another user #99 tries to inspect it without canViewAll=true
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            requestService.GetRequestByIdAsync(created.AccessRequestId, callerUserId: 99, canViewAll: false));
    }

    [Fact]
    public async Task CreateRequest_DuplicatePending_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(CreateRequest_DuplicatePending_ThrowsInvalidOperationException));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var dto = new CreateAccessRequestDto
        {
            ResourceName = "SQL-PROD-DUPLICATE",
            Reason = "Initial maintenance request"
        };

        // First creation succeeds
        await requestService.CreateRequestAsync(10, dto, "10.0.0.1");

        // Act & Assert: Duplicate submission while first is still pending
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            requestService.CreateRequestAsync(10, dto, "10.0.0.1"));

        Assert.Contains("Duplicate submissions are not permitted", ex.Message);
    }

    [Fact]
    public async Task CreateRequest_PastExpiration_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(CreateRequest_PastExpiration_ThrowsArgumentException));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var dto = new CreateAccessRequestDto
        {
            ResourceName = "SQL-PROD-EXPIRED",
            Reason = "Attempting to request access in the past",
            StartAt = DateTime.UtcNow.AddHours(-3),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            requestService.CreateRequestAsync(10, dto, "10.0.0.1"));

        Assert.Contains("Expiration date cannot be in the past", ex.Message);
    }

    [Fact]
    public async Task ApproveRequest_PastExpiration_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryContext(nameof(ApproveRequest_PastExpiration_ThrowsArgumentException));
        var auditService = new AuditLogService(context, NullLogger<AuditLogService>.Instance);
        var requestService = new AccessRequestService(context, auditService);

        var created = await requestService.CreateRequestAsync(10, new CreateAccessRequestDto
        {
            ResourceName = "K8S-EXPIRED-TEST",
            Reason = "Cluster maintenance"
        }, "10.0.0.1");

        var approveDto = new ApproveAccessRequestDto
        {
            StartAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-30), // Past expiration
            Comment = "Attempted backdated approval"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            requestService.ApproveRequestAsync(created.AccessRequestId, 20, approveDto, "10.0.0.2"));

        Assert.Contains("Granted expiration time must be in the future", ex.Message);
    }
}

