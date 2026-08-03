using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Persistence.Seeders;

public class AdminSeeder(
    ApplicationDbContext db,
    IPasswordService passwordService,
    ILogger<AdminSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        const string email = "4sinchrony@gmail.com";

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existing is not null)
        {
            logger.LogInformation("AdminSeeder: admin {Email} already exists.", email);
            return;
        }

        var hash = passwordService.HashPassword("clubevip123");
        var admin = User.Create("4Sinchrony Admin", email, null, hash, Role.admin);
        admin.SetGlobalAdmin(true);

        await db.Users.AddAsync(admin, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("AdminSeeder: admin {Email} created successfully.", email);
    }
}