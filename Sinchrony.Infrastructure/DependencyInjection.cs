using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sinchrony.Application.Payments.Commands;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Persistence;
using Sinchrony.Infrastructure.Persistence.Repositories;
using Sinchrony.Infrastructure.Services;

namespace Sinchrony.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(opt =>
            opt.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IPackageRepository, PackageRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<IStudioRepository, StudioRepository>();
        services.AddScoped<IBikeRepository, BikeRepository>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IReferralRepository, ReferralRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IClassTypeRepository, ClassTypeRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ICreditTransactionRepository, CreditTransactionRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddSingleton<IAppSettings, AppSettings>();
        services.AddScoped<IPackageTypeRepository, PackageTypeRepository>();
        services.AddScoped<IBenefitRepository, BenefitRepository>();
        services.AddScoped<IStudentPackageRepository, StudentPackageRepository>();
        services.AddScoped<IDependentRepository, DependentRepository>();
        services.AddScoped<IDependentPackageAllocationRepository, DependentPackageAllocationRepository>();
        services.AddScoped<PurchasePackageService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IUnitRepository, UnitRepository>();
        services.AddScoped<IUnitContext, UnitContext>();

        services.AddHttpClient<IStorageService, SupabaseStorageService>(client =>
        {
            client.DefaultRequestHeaders.Add("Authorization",
                $"Bearer {configuration["Storage:Key"]}");
            client.DefaultRequestHeaders.Add("apikey", configuration["Storage:Key"]!);
        });

        // Domain services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordService, PasswordService>();

        // Asaas HTTP client
        services.AddHttpClient<IAsaasService, AsaasService>(client =>
        {
            client.DefaultRequestHeaders.Add("access_token", configuration["Asaas:ApiKey"]);
            client.DefaultRequestHeaders.Add("User-Agent", "4Sinchrony/1.0");
        });

        return services;
    }
}