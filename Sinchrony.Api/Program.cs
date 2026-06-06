using AspNetCoreRateLimit;
using Sinchrony.Api.Extensions;
using Sinchrony.Api.Middlewares;
using Sinchrony.Application;
using Sinchrony.Infrastructure;
using Sinchrony.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
            logger.LogInformation("Attempt {A}: applying migrations...", attempts + 1);
            db.Database.Migrate();
            logger.LogInformation("Migrations applied successfully.");
            break;
        }
        catch (Exception ex)
        {
            attempts++;
            logger.LogWarning("Attempt {A}/5 failed: {Error}", attempts, ex.Message);
            if (attempts == 5)
            {
                logger.LogError("Failed to apply migrations after 5 attempts.");
                throw;
            }
            Thread.Sleep(3000);
        }
    }
}

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