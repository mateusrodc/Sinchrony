using System;
using System.Collections.Generic;
using System.Text;

namespace Sinchrony.Domain.Interfaces.Services
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
    }
}
