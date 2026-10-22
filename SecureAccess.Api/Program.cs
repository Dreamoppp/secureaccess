using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SecureAccess.Api.Authorization;
using SecureAccess.Api.Data;
using SecureAccess.Api.Middleware;
using SecureAccess.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------------------
// 1. Controller Services & Model Validation
// ----------------------------------------------------------------------------
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Keep model validation consistent with centralized API response schema
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            var response = SecureAccess.Api.DTOs.Common.ApiResponse<object>.Fail("Validation failed.", errors);
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
        };
    });

builder.Services.AddEndpointsApiExplorer();

// ----------------------------------------------------------------------------
// 2. Swagger / OpenAPI Configuration (with JWT Bearer Support)
// ----------------------------------------------------------------------------
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SecureAccess Privileged Access Management API",
        Version = "v1",
        Description = "Enterprise REST API for SecureAccess PAM System supporting C# WinForms desktop clients."
    });

    // Add JWT Bearer authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ----------------------------------------------------------------------------
// 3. Database Provider (EF Core SQL Server with InMemory Fallback Support)
// ----------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("SecureAccessConnection");
var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase")
                  || Environment.GetEnvironmentVariable("USE_INMEMORY_DB") == "true";

builder.Services.AddDbContext<SecureAccessDbContext>(options =>
{
    if (useInMemory || string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseInMemoryDatabase("SecureAccessInMemoryDb");
    }
    else
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });
    }
});

// ----------------------------------------------------------------------------
// 4. JWT Authentication
// ----------------------------------------------------------------------------
const string defaultDevKey = "SecureAccessEnterprisePAMSystemDevSecretKey2026!#$@MustBe32BytesLongMinimum";
var jwtKey = builder.Configuration["Jwt:Key"] ?? defaultDevKey;

if (!builder.Environment.IsDevelopment() && (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]) || builder.Configuration["Jwt:Key"] == defaultDevKey))
{
    throw new InvalidOperationException("CRITICAL SECURITY VIOLATION: The production environment cannot run with the default development JWT signing key. Set 'Jwt:Key' via an environment variable (Jwt__Key) or Azure Key Vault / Secret Manager.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SecureAccessApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SecureAccessClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

// ----------------------------------------------------------------------------
// 5. Policy-Based Authorization (RBAC Permissions)
// ----------------------------------------------------------------------------
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.Requirements.Add(new PermissionRequirement(permission)));
    }
});

// ----------------------------------------------------------------------------
// 6. Application Services & Dependency Injection
// ----------------------------------------------------------------------------
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAccessRequestService, AccessRequestService>();

var app = builder.Build();

// ----------------------------------------------------------------------------
// 7. Request Pipeline & Middleware
// ----------------------------------------------------------------------------
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SecureAccess API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ----------------------------------------------------------------------------
// 8. Safe Database Initialization
// ----------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var dbContext = services.GetRequiredService<SecureAccessDbContext>();

    try
    {
        if (useInMemory)
        {
            logger.LogInformation("Operating in InMemory database mode.");
            dbContext.Database.EnsureCreated();
            await DatabaseSeeder.SeedAsync(dbContext, logger);
        }
        else
        {
            var canConnect = await dbContext.Database.CanConnectAsync();
            if (canConnect)
            {
                logger.LogInformation("Successfully connected to SQL Server (SecureAccessDB).");
                await DatabaseSeeder.SeedAsync(dbContext, logger);
            }
            else
            {
                logger.LogWarning("SQL Server is currently unreachable. The API is ready; verify connection string or start SQL Server when available.");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning("Database check encountered: {Message}. The API will continue running.", ex.Message);
    }
}

app.Run();

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }
