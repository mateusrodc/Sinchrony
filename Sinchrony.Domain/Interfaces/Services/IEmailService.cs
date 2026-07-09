using Sinchrony.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sinchrony.Domain.Interfaces.Services
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
        Task SendWithSettingsAsync(string to, string subject, string body, Settings? settings, CancellationToken ct = default);
    }
}
