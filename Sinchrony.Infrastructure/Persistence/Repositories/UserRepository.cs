using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class UserRepository(ApplicationDbContext db) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Users
            .Include(u => u.RefreshTokens)
            .Include(u => u.Cards)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email == email.ToLower(), ct);

    public async Task<User?> GetByRefreshTokenAsync(string token, CancellationToken ct = default)
        => await db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == token), ct);

    public async Task<IEnumerable<User>> ListStudentsAsync(string? status, CancellationToken ct = default)
    {
        var query = db.Users.Where(u => u.Role == Domain.Enums.Role.student);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(u => u.Status.ToString() == status);
        return await query.OrderBy(u => u.Name).ToListAsync(ct);
    }

    public async Task<IEnumerable<User>> ListTeachersAsync(bool? active, CancellationToken ct = default)
    {
        var query = db.Users.Where(u => u.Role == Domain.Enums.Role.teacher);
        if (active.HasValue) query = query.Where(u => u.Active == active.Value);
        return await query.OrderBy(u => u.Name).ToListAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await db.Users.AddAsync(user, ct);

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct = default)
        => await db.RefreshTokens.AddAsync(refreshToken, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    public async Task<(IEnumerable<User> Items, int Total)> ListStudentsPagedAsync(
    string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Users.Where(u => u.Role == Domain.Enums.Role.student);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(u => u.Status.ToString() == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
    public async Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken ct = default)
    => await db.Users
        .Include(u => u.RefreshTokens)
        .FirstOrDefaultAsync(u => u.GoogleId == googleId, ct);

    public async Task<IEnumerable<User>> ListStudentsByUnitAsync(Guid unitId, CancellationToken ct = default)
    => await db.Users
        .Where(u => u.Role == Role.student && u.UnitId == unitId)
        .OrderBy(u => u.Name)
        .ToListAsync(ct);

    public async Task<IEnumerable<User>> ListTeachersByUnitAsync(Guid unitId, CancellationToken ct = default)
        => await db.Users
            .Where(u => u.Role == Role.teacher && u.UnitId == unitId)
            .OrderBy(u => u.Name)
            .ToListAsync(ct);
}