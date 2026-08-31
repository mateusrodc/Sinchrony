using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Sinchrony.Domain.Exceptions;

namespace Sinchrony.Api.Middlewares;

public class ExceptionMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            Log.Warning("Domain exception: {Code} — {Message} | Path: {Path}",
                ex.Code, ex.Message, context.Request.Path);

            context.Response.StatusCode = ex.HttpStatus;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new { code = ex.Code, message = ex.Message }
            }));
        }
        catch (ValidationException ex)
        {
            Log.Warning("Validation exception | Path: {Path} | Errors: {Errors}",
                context.Request.Path,
                string.Join(", ", ex.Errors.Select(e => e.ErrorMessage)));

            context.Response.StatusCode = 422;
            context.Response.ContentType = "application/json";
            var errors = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new { code = "VALIDATION_ERROR", message = "Validation failed.", details = errors }
            }));
        }
        catch (DbUpdateException ex)
        {
            // Cobre corrida entre a checagem de duplicidade (email/CPF/etc.) feita no handler
            // e o INSERT/UPDATE real: dois requests concorrentes podem passar os dois pela
            // checagem e só um vencer o índice único no banco. Sem isso, o violador cai no
            // catch genérico abaixo e vira 500 em vez de um 409 compreensível pro cliente.
            Log.Warning(ex, "DbUpdateException (provável conflito de concorrência) | Path: {Path}",
                context.Request.Path);

            context.Response.StatusCode = 409;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new
                {
                    code = "CONFLICT",
                    message = "Este registro já existe ou entrou em conflito com outra alteração recente. Tente novamente."
                }
            }));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception | Path: {Path}", context.Request.Path);

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new { code = "INTERNAL_ERROR", message = "Internal server error." }
            }));
        }
    }
}