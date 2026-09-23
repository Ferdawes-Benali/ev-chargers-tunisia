using Microsoft.AspNetCore.Mvc;

namespace EvChargers.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        => (_next, _logger) = (next, logger);

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = 500;
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 500,
                Title = "An error occurred",
                Detail = "Please try again later."
            });
        }
    }
}