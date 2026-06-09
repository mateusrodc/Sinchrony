using AspNetCoreRateLimit;
using Serilog;
using Serilog.Events;
using Sinchrony.Api.Extensions;
using Sinchrony.Api.Middlewares;
using Sinchrony.Application;
using Sinchrony.Infrastructure;
using Sinchrony.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

// Serilog bootstrap logger — captura erros antes do host iniciar
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting 4Sinchrony API...");

    var builder = WebApplication.CreateBuilder(args);

    // Substitui o logging padrão pelo Serilog
    builder.Host.UseSerilog((context, services, config) =>
    {
        var isProduction = context.HostingEnvironment.IsProduction();

        config
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId();

        if (isProduction)
        {
            // Produção: JSON estruturado (fácil de parsear no Render/Datadog/etc)
            config.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
        }
        else
        {
            // Dev: formato legível
            config.WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }
    });

    builder.Services.AddApiExtensions(builder.Configuration);
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database");

    var app = builder.Build();

    // Migrations automáticas com retry
    using (var scope = app.Services.CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var attempts = 0;
        while (attempts < 5)
        {
            try
            {
                logger.LogInformation("Attempt {Attempt}: applying migrations...", attempts + 1);
                db.Database.Migrate();
                logger.LogInformation("Migrations applied successfully.");
                break;
            }
            catch (Exception ex)
            {
                attempts++;
                logger.LogWarning("Attempt {Attempt}/5 failed: {Error}", attempts, ex.Message);
                if (attempts == 5)
                {
                    logger.LogError("Failed to apply migrations after 5 attempts.");
                    throw;
                }
                Thread.Sleep(3000);
            }
        }
    }

    // Log de cada requisição HTTP
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        opts.GetLevel = (ctx, elapsed, ex) =>
            ex != null || ctx.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : ctx.Response.StatusCode >= 400
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
    });

    app.UseMiddleware<ExceptionMiddleware>();
    app.UseMiddleware<RequestIdMiddleware>();
    app.UseIpRateLimiting();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "4Sinchrony API v1");
            c.RoutePrefix = string.Empty;
        });
    }

    if (app.Environment.IsProduction())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "4Sinchrony API v1");
            c.RoutePrefix = "docs";
        });
    }

    app.UseCors("DefaultPolicy");

    if (app.Environment.IsDevelopment())
        app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHealthChecks("/health");
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}