using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Entities;

namespace SecureAccess.Api.Data;

public class SecureAccessDbContext : DbContext
{
    public SecureAccessDbContext(DbContextOptions<SecureAccessDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------------------------------------------------------------------
        // 1. Users
        // ---------------------------------------------------------------------
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.UserId);

            entity.Property(u => u.FullName).HasMaxLength(150).IsRequired();
            entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(u => u.IsActive).HasDefaultValue(true);
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(u => u.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        // ---------------------------------------------------------------------
        // 2. Roles
        // ---------------------------------------------------------------------
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(r => r.RoleId);

            entity.Property(r => r.RoleName).HasMaxLength(50).IsRequired();
            entity.Property(r => r.Description).HasMaxLength(250);
            entity.Property(r => r.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(r => r.RoleName).IsUnique();
        });

        // ---------------------------------------------------------------------
        // 3. UserRoles (Junction: Many-to-Many Users <-> Roles)
        // ---------------------------------------------------------------------
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.Property(ur => ur.AssignedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(ur => ur.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(ur => ur.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ur => ur.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(ur => ur.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------------------------------------------------------------
        // 4. Permissions
        // ---------------------------------------------------------------------
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(p => p.PermissionId);

            entity.Property(p => p.PermissionName).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Resource).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(250);
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(p => p.PermissionName).IsUnique();
        });

        // ---------------------------------------------------------------------
        // 5. RolePermissions (Junction: Many-to-Many Roles <-> Permissions)
        // ---------------------------------------------------------------------
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            entity.Property(rp => rp.AssignedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(rp => rp.Role)
                  .WithMany(r => r.RolePermissions)
                  .HasForeignKey(rp => rp.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rp => rp.Permission)
                  .WithMany(p => p.RolePermissions)
                  .HasForeignKey(rp => rp.PermissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------------------------------------------------------------
        // 6. AccessRequests
        // ---------------------------------------------------------------------
        modelBuilder.Entity<AccessRequest>(entity =>
        {
            entity.ToTable("AccessRequests");
            entity.HasKey(ar => ar.AccessRequestId);

            entity.Property(ar => ar.ResourceName).HasMaxLength(100).IsRequired();
            entity.Property(ar => ar.Reason).HasMaxLength(500).IsRequired();
            entity.Property(ar => ar.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
            entity.Property(ar => ar.DecisionComment).HasMaxLength(500);
            entity.Property(ar => ar.RequestedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(ar => ar.RequesterUser)
                  .WithMany(u => u.RequestedAccesses)
                  .HasForeignKey(ar => ar.RequesterUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ar => ar.ApproverUser)
                  .WithMany(u => u.DecidedAccesses)
                  .HasForeignKey(ar => ar.ApproverUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------------------------------------------------------------
        // 7. AuditLogs (Preserve logs with ON DELETE SET NULL)
        // ---------------------------------------------------------------------
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(al => al.AuditLogId);

            entity.Property(al => al.Action).HasMaxLength(100).IsRequired();
            entity.Property(al => al.ResourceType).HasMaxLength(100).IsRequired();
            entity.Property(al => al.ResourceId).HasMaxLength(100);
            entity.Property(al => al.Details).IsRequired();
            entity.Property(al => al.IpAddress).HasMaxLength(45);
            entity.Property(al => al.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(al => al.User)
                  .WithMany(u => u.AuditLogs)
                  .HasForeignKey(al => al.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
