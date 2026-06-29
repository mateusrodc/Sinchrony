using Sinchrony.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sinchrony.Domain.Interfaces.Repositories
{
    public interface IPasswordResetTokenRepository
    {
        Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken ct = default);
        Task AddAsync(PasswordResetToken token, CancellationToken ct = default);
        Task SaveAsync(CancellationToken ct = default);
    }
}
