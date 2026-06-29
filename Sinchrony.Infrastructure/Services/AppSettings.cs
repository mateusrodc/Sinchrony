using Microsoft.Extensions.Configuration;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

public class AppSettings(IConfiguration configuration) : IAppSettings
{
    public string ErpUrl => configuration["ErpUrl"] ?? "https://erp.sinchrony.com";
}