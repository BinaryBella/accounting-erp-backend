using AccountingERP.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Middleware;

/// <summary>
/// Single place every unhandled exception is turned into an RFC 7807 response
/// (PLAN §7). Known application exceptions map to 4xx; everything else is a 500
/// whose detail is logged, never returned, outside Development.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            UnbalancedJournalException => (StatusCodes.Status422UnprocessableEntity, "Journal entry does not balance"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("{Exception} on {Method} {Path}: {Message}",
                exception.GetType().Name, context.Request.Method, context.Request.Path, exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.io/{status}",
            Instance = context.Request.Path,
            Detail = status == StatusCodes.Status500InternalServerError && !_environment.IsDevelopment()
                ? "See the server logs for details."
                : exception.Message
        };

        if (exception is ValidationException { Errors.Count: > 0 } validation)
            problem.Extensions["errors"] = validation.Errors;

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response already started; cannot write ProblemDetails for {Exception}.", exception.GetType().Name);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, problem.GetType());
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
