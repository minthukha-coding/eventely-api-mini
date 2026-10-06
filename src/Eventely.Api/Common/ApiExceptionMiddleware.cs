using Microsoft.AspNetCore.Mvc;

namespace Eventely.Api.Common;

public sealed class ApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client has disconnected; there is no response to write.
        }
        catch (ApiException exception)
        {
            logger.LogInformation("Request failed with {StatusCode}: {Message}", exception.StatusCode, exception.Message);
            await WriteProblemAsync(context, exception.StatusCode, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled API exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode >= 500 ? "Request failed" : "Request could not be completed",
            Detail = detail,
            Instance = context.Request.Path
        }, cancellationToken: context.RequestAborted);
    }
}
