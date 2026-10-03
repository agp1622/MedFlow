using System.Net;
using System.Text.Json;
using MedFlow.Api.Localization;

namespace MedFlow.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // Exception text is internal and English only; users get a localized generic message of the same status
        var (statusCode, message) = ex switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, Localizer.Get(context, "Error.NotFound")),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, Localizer.Get(context, "Error.AccessDenied")),
            ArgumentException => (HttpStatusCode.BadRequest, Localizer.Get(context, "Error.BadRequest")),
            _ => (HttpStatusCode.InternalServerError, Localizer.Get(context, "Error.Unexpected"))
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var body = JsonSerializer.Serialize(new { error = message, statusCode = (int)statusCode });
        return context.Response.WriteAsync(body);
    }
}
