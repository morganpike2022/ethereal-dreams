using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace MMORPG.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, ex);
        }
    }

    private static Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            KeyNotFoundException        => (HttpStatusCode.NotFound,     "Not Found"),
            InvalidOperationException   => (HttpStatusCode.Conflict,     "Conflict"),
            ArgumentException           => (HttpStatusCode.BadRequest,   "Bad Request"),
            _                           => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        context.Response.StatusCode  = (int)status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title  = title,
            Detail = ex is not Exception { } e || (int)status == 500 ? null : e.Message
        };

        return context.Response.WriteAsJsonAsync(problem);
    }
}
