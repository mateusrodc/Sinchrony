using Sinchrony.Domain.Enums;

namespace Sinchrony.Domain.Entities;

public class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public Role Role { get; private set; }
    public int Credits { get; private set; }
    public string? Avatar { get; private set; }
    public StudentStatus Status { get; private set; } = StudentStatus.active;
    public bool Active { get; private set; } = true;
    public string? PlanName { get; private set; }
    public string? ReferralCode { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public string? Cpf { get; private set; }
    public string? GoogleId { get; private set; }

    public ICollection<RefreshToken> RefreshTokens { get; private set; } = [];
    public ICollection<Booking> Bookings { get; private set; } = [];
    public ICollection<Purchase> Purchases { get; private set; } = [];
    public ICollection<Card> Cards { get; private set; } = [];

    protected User() { }

    public static User Create(string name, string email, string? phone, string passwordHash, Role role, string? cpf = null)
    {
        return new User
        {
            Name = name,
            Email = email.ToLower(),
            Phone = phone,
            PasswordHash = passwordHash,
            Role = role,
            Credits = 0,
            ReferralCode = GenerateReferralCode(name),
            Cpf = cpf,
        };
    }

    public void UpdateProfile(string name, string email, string? phone, string? avatar)
    {
        Name = name;
        Email = email.ToLower();
        Phone = phone;
        Avatar = avatar;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePassword(string newHash)
    {
        PasswordHash = newHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddCredits(int amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive.");
        Credits += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DeductCredits(int amount)
    {
        if (amount > Credits) throw new InvalidOperationException("Insufficient credits.");
        Credits -= amount;
        UpdatedAt = DateTime.UtcNow;
    }
    public void UpdatePlan(string? plan)
    {
        PlanName = plan;
        UpdatedAt = DateTime.UtcNow;
    }

    public static User CreateWithGoogle(string name, string email, string googleId, string? avatar = null)
    {
        return new User
        {
            Name = name,
            Email = email.ToLower(),
            GoogleId = googleId,
            Avatar = avatar,
            PasswordHash = Guid.NewGuid().ToString(), // senha aleatória
            Role = Role.student,
            Credits = 0,
            ReferralCode = GenerateReferralCode(name)
        };
    }

    public void LinkGoogle(string googleId)
    {
        GoogleId = googleId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() { Status = StudentStatus.inactive; Active = false; UpdatedAt = DateTime.UtcNow; }
    public void Reactivate() { Status = StudentStatus.active; Active = true; UpdatedAt = DateTime.UtcNow; }
    public void Block() { Status = StudentStatus.blocked; UpdatedAt = DateTime.UtcNow; }

    private static string GenerateReferralCode(string name)
    {
        var prefix = name.Length >= 5 ? name[..5].ToUpper() : name.ToUpper();
        return prefix + Random.Shared.Next(100, 999);
    }
    public void UpdateCpf(string? cpf)
    {
        Cpf = string.IsNullOrEmpty(cpf) ? null : cpf.Replace(".", "").Replace("-", "").Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}