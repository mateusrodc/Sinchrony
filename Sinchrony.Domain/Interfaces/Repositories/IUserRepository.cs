using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByRefreshTokenAsync(string token, CancellationToken ct = default);
    Task<IEnumerable<User>> ListStudentsAsync(string? status, CancellationToken ct = default);
    Task<IEnumerable<User>> ListTeachersAsync(bool? active, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct = default);
    Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task<(IEnumerable<User> Items, int Total)> ListStudentsPagedAsync(
    string? status, int page, int pageSize, CancellationToken ct = default);

    Task<IEnumerable<User>> ListStudentsByUnitAsync(Guid unitId, CancellationToken ct = default);
    Task<IEnumerable<User>> ListTeachersByUnitAsync(Guid unitId, CancellationToken ct = default);
}