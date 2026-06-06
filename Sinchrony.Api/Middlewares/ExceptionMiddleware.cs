using System.Text.Json;
using FluentValidation;
using Sinchrony.Domain.Exceptions;

namespace Sinchrony.Api.Middlewares;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            context.Response.StatusCode = ex.HttpStatus;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new { code = ex.Code, message = ex.Message }
            }));
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = 422;
            context.Response.ContentType = "application/json";
            var errors = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new { code = "VALIDATION_ERROR", message = "Validation failed.", details = errors }
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception.");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new { code = "INTERNAL_ERROR", message = "Internal server error." }
            }));
        }
    }
}