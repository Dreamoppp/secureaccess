using System.Net;
using System.Text.Json;
using SecureAccess.Api.DTOs.Common;

namespace SecureAccess.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred during request execution: {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            KeyNotFoundException knf => (HttpStatusCode.NotFound, knf.Message),
            UnauthorizedAccessException uae => (HttpStatusCode.Forbidden, uae.Message),
            InvalidOperationException ioe => (HttpStatusCode.BadRequest, ioe.Message),
            ArgumentException ae => (HttpStatusCode.BadRequest, ae.Message),
            TimeoutException => (HttpStatusCode.ServiceUnavailable, "The request timed out. Please try again later."),
            _ when exception.GetType().Name.Contains("Sql") || exception.GetType().Name.Contains("DbUpdate") =>
                (HttpStatusCode.ServiceUnavailable, "The database service is temporarily unavailable. Please retry in a few moments or contact the administrator."),
            _ => (HttpStatusCode.InternalServerError, "An unexpected internal server error occurred. Please contact the administrator.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse<object>.Fail(message, new List<string> { message });
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
