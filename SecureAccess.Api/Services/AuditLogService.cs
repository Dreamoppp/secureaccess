using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Data;
using SecureAccess.Api.DTOs.AuditLogs;
using SecureAccess.Api.DTOs.Common;
using SecureAccess.Api.Entities;

namespace SecureAccess.Api.Services;

public interface IAuditLogService
{
    Task LogAsync(int? userId, string action, string resourceType, string? resourceId, string details, string? ipAddress);
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(int pageNumber = 1, int pageSize = 20, string? action = null, string? resourceType = null);
}

public class AuditLogService : IAuditLogService
{
    private readonly SecureAccessDbContext _context;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(SecureAccessDbContext context, ILogger<AuditLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(
        int? userId,
        string action,
        string resourceType,
        string? resourceId,
        string details,
        string? ipAddress)
    {
        try
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Details = details,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Logging failure should be reported in application logs but not tear down active user request
            _logger.LogError(ex, "Failed to write audit log for Action: {Action}, User: {UserId}", action, userId);
        }
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? action = null,
        string? resourceType = null)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action.Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            query = query.Where(a => a.ResourceType == resourceType);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto
            {
                AuditLogId = a.AuditLogId,
                UserId = a.UserId,
                Username = a.User != null ? a.User.Username : "[SYSTEM / UNAUTH]",
                Action = a.Action,
                ResourceType = a.ResourceType,
                ResourceId = a.ResourceId,
                Details = a.Details,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<AuditLogDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
