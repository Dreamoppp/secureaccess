using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Data;
using SecureAccess.Api.DTOs.AccessRequests;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.Entities;

namespace SecureAccess.Api.Services;

public interface IAccessRequestService
{
    Task<PagedResult<AccessRequestDto>> GetRequestsAsync(
        int callerUserId,
        bool canViewAll,
        string? status = null,
        int? requesterId = null,
        int pageNumber = 1,
        int pageSize = 20);

    Task<AccessRequestDto?> GetRequestByIdAsync(int id, int callerUserId, bool canViewAll);

    Task<AccessRequestDto> CreateRequestAsync(int requesterUserId, CreateAccessRequestDto dto, string? ipAddress);

    Task<AccessRequestDto> ApproveRequestAsync(int requestId, int approverUserId, ApproveAccessRequestDto dto, string? ipAddress);

    Task<AccessRequestDto> RejectRequestAsync(int requestId, int approverUserId, RejectAccessRequestDto dto, string? ipAddress);

    Task<AccessRequestDto> RevokeRequestAsync(int requestId, int approverUserId, RevokeAccessRequestDto dto, string? ipAddress);
}

public class AccessRequestService : IAccessRequestService
{
    private readonly SecureAccessDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AccessRequestService(SecureAccessDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<AccessRequestDto>> GetRequestsAsync(
        int callerUserId,
        bool canViewAll,
        string? status = null,
        int? requesterId = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var query = _context.AccessRequests
            .Include(ar => ar.RequesterUser)
            .Include(ar => ar.ApproverUser)
            .AsNoTracking();

        // Security boundary: If caller cannot view all requests, restrict to their own
        if (!canViewAll)
        {
            query = query.Where(ar => ar.RequesterUserId == callerUserId);
        }
        else if (requesterId.HasValue)
        {
            query = query.Where(ar => ar.RequesterUserId == requesterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(ar => ar.Status == status);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(ar => ar.RequestedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(ar => MapToDto(ar))
            .ToListAsync();

        return new PagedResult<AccessRequestDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AccessRequestDto?> GetRequestByIdAsync(int id, int callerUserId, bool canViewAll)
    {
        var request = await _context.AccessRequests
            .Include(ar => ar.RequesterUser)
            .Include(ar => ar.ApproverUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(ar => ar.AccessRequestId == id);

        if (request == null)
        {
            return null;
        }

        // Restrict regular users from inspecting requests owned by other users
        if (!canViewAll && request.RequesterUserId != callerUserId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this access request.");
        }

        return MapToDto(request);
    }

    public async Task<AccessRequestDto> CreateRequestAsync(int requesterUserId, CreateAccessRequestDto dto, string? ipAddress)
    {
        var requester = await _context.Users.FindAsync(requesterUserId);
        if (requester == null || !requester.IsActive)
        {
            throw new InvalidOperationException("Requesting user account is inactive or not found.");
        }

        if (dto.StartAt.HasValue && dto.ExpiresAt.HasValue && dto.StartAt > dto.ExpiresAt)
        {
            throw new ArgumentException("Start date cannot be after expiration date.");
        }

        if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value < DateTime.UtcNow.AddMinutes(-5))
        {
            throw new ArgumentException("Expiration date cannot be in the past.");
        }

        var resourceTrimmed = dto.ResourceName.Trim();

        // Enforce anti-duplicate pending request protection
        var hasDuplicatePending = await _context.AccessRequests
            .AnyAsync(ar => ar.RequesterUserId == requesterUserId
                         && ar.ResourceName == resourceTrimmed
                         && ar.Status == "Pending");
        if (hasDuplicatePending)
        {
            throw new InvalidOperationException($"A pending access request already exists for resource '{resourceTrimmed}'. Duplicate submissions are not permitted.");
        }

        var accessRequest = new AccessRequest
        {
            RequesterUserId = requesterUserId,
            ResourceName = resourceTrimmed,
            Reason = dto.Reason.Trim(),
            RequestedAt = DateTime.UtcNow,
            StartAt = dto.StartAt,
            ExpiresAt = dto.ExpiresAt,
            Status = "Pending"
        };

        _context.AccessRequests.Add(accessRequest);
        await _context.SaveChangesAsync();

        // Audit log creation
        await _auditLogService.LogAsync(
            requesterUserId,
            "ACCESS_REQUEST_CREATED",
            "AccessRequests",
            accessRequest.AccessRequestId.ToString(),
            $"User {requester.Username} created pending access request for resource '{accessRequest.ResourceName}'.",
            ipAddress);

        return await GetRequestByIdAsync(accessRequest.AccessRequestId, requesterUserId, true)
            ?? throw new InvalidOperationException("Failed to retrieve created access request.");
    }

    public async Task<AccessRequestDto> ApproveRequestAsync(
        int requestId,
        int approverUserId,
        ApproveAccessRequestDto dto,
        string? ipAddress)
    {
        var request = await _context.AccessRequests
            .Include(ar => ar.RequesterUser)
            .FirstOrDefaultAsync(ar => ar.AccessRequestId == requestId);

        if (request == null)
        {
            throw new KeyNotFoundException($"Access request #{requestId} was not found.");
        }

        if (request.Status != "Pending")
        {
            throw new InvalidOperationException($"Cannot approve request #{requestId} because its current status is '{request.Status}'. Only 'Pending' requests can be approved.");
        }

        // Enforce strict Self-Approval Prevention Rule
        if (request.RequesterUserId == approverUserId)
        {
            throw new InvalidOperationException("Self-Approval Violation: Approvers are strictly prohibited from approving their own access requests.");
        }

        var approver = await _context.Users.FindAsync(approverUserId);
        if (approver == null || !approver.IsActive)
        {
            throw new InvalidOperationException("Approver account is inactive or not found.");
        }

        var now = DateTime.UtcNow;
        var startAt = dto.StartAt ?? now;
        var expiresAt = dto.ExpiresAt ?? startAt.AddHours(4); // Default 4-hour PAM window

        if (startAt > expiresAt)
        {
            throw new ArgumentException("Granted start time cannot be after expiration time.");
        }

        if (expiresAt <= now)
        {
            throw new ArgumentException("Granted expiration time must be in the future.");
        }

        request.Status = "Approved";
        request.ApproverUserId = approverUserId;
        request.DecisionAt = now;
        request.DecisionComment = dto.Comment?.Trim();
        request.StartAt = startAt;
        request.ExpiresAt = expiresAt;

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            approverUserId,
            "ACCESS_REQUEST_APPROVED",
            "AccessRequests",
            requestId.ToString(),
            $"Approver {approver.Username} approved request #{requestId} for resource '{request.ResourceName}'. Window: {startAt:u} to {expiresAt:u}.",
            ipAddress);

        return (await GetRequestByIdAsync(requestId, approverUserId, true))!;
    }

    public async Task<AccessRequestDto> RejectRequestAsync(
        int requestId,
        int approverUserId,
        RejectAccessRequestDto dto,
        string? ipAddress)
    {
        var request = await _context.AccessRequests
            .Include(ar => ar.RequesterUser)
            .FirstOrDefaultAsync(ar => ar.AccessRequestId == requestId);

        if (request == null)
        {
            throw new KeyNotFoundException($"Access request #{requestId} was not found.");
        }

        if (request.Status != "Pending")
        {
            throw new InvalidOperationException($"Cannot reject request #{requestId} because its current status is '{request.Status}'. Only 'Pending' requests can be rejected.");
        }

        var approver = await _context.Users.FindAsync(approverUserId);
        if (approver == null || !approver.IsActive)
        {
            throw new InvalidOperationException("Approver account is inactive or not found.");
        }

        request.Status = "Rejected";
        request.ApproverUserId = approverUserId;
        request.DecisionAt = DateTime.UtcNow;
        request.DecisionComment = dto.Comment.Trim();

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            approverUserId,
            "ACCESS_REQUEST_REJECTED",
            "AccessRequests",
            requestId.ToString(),
            $"Approver {approver.Username} rejected request #{requestId} for resource '{request.ResourceName}'. Reason: {request.DecisionComment}",
            ipAddress);

        return (await GetRequestByIdAsync(requestId, approverUserId, true))!;
    }

    public async Task<AccessRequestDto> RevokeRequestAsync(
        int requestId,
        int approverUserId,
        RevokeAccessRequestDto dto,
        string? ipAddress)
    {
        var request = await _context.AccessRequests
            .Include(ar => ar.RequesterUser)
            .FirstOrDefaultAsync(ar => ar.AccessRequestId == requestId);

        if (request == null)
        {
            throw new KeyNotFoundException($"Access request #{requestId} was not found.");
        }

        if (request.Status != "Approved")
        {
            throw new InvalidOperationException($"Cannot revoke request #{requestId} because its status is '{request.Status}'. Only 'Approved' requests can be revoked.");
        }

        var approver = await _context.Users.FindAsync(approverUserId);
        if (approver == null || !approver.IsActive)
        {
            throw new InvalidOperationException("Revoking account is inactive or not found.");
        }

        request.Status = "Revoked";
        request.ExpiresAt = DateTime.UtcNow; // Truncate access window immediately
        request.DecisionComment = string.IsNullOrWhiteSpace(request.DecisionComment)
            ? $"Revocation: {dto.Comment.Trim()}"
            : $"{request.DecisionComment} | Revocation: {dto.Comment.Trim()}";

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            approverUserId,
            "ACCESS_REQUEST_REVOKED",
            "AccessRequests",
            requestId.ToString(),
            $"Approver {approver.Username} revoked active access for request #{requestId} on resource '{request.ResourceName}'. Reason: {dto.Comment}",
            ipAddress);

        return (await GetRequestByIdAsync(requestId, approverUserId, true))!;
    }

    private static AccessRequestDto MapToDto(AccessRequest ar)
    {
        return new AccessRequestDto
        {
            AccessRequestId = ar.AccessRequestId,
            RequesterUserId = ar.RequesterUserId,
            RequesterUsername = ar.RequesterUser?.Username ?? string.Empty,
            RequesterFullName = ar.RequesterUser?.FullName ?? string.Empty,
            ResourceName = ar.ResourceName,
            Reason = ar.Reason,
            RequestedAt = ar.RequestedAt,
            StartAt = ar.StartAt,
            ExpiresAt = ar.ExpiresAt,
            Status = ar.Status,
            ApproverUserId = ar.ApproverUserId,
            ApproverUsername = ar.ApproverUser?.Username,
            ApproverFullName = ar.ApproverUser?.FullName,
            DecisionAt = ar.DecisionAt,
            DecisionComment = ar.DecisionComment,
            IsActiveGrant = ar.IsActiveGrant()
        };
    }
}
