using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Studio> Studios => Set<Studio>();
    public DbSet<ClassType> ClassTypes => Set<ClassType>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<Bike> Bikes => Set<Bike>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}