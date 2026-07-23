namespace Sinchrony.Domain.Entities;

public class Dependent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ResponsibleStudentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly? BirthDate { get; private set; }
    public string? Cpf { get; private set; }
    public Guid? UserId { get; private set; } // reservado pro futuro
    public bool CanBook { get; private set; } = true;
    public bool CanCancel { get; private set; } = true;
    public bool CanViewHistory { get; private set; } = true;
    public bool Active { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public User? ResponsibleStudent { get; private set; }
    public User? User { get; private set; }

    protected Dependent() { }

    public static Dependent Create(
        Guid responsibleStudentId, string name,
        DateOnly? birthDate = null, string? cpf = null)
        => new()
        {
            ResponsibleStudentId = responsibleStudentId,
            Name = name,
            BirthDate = birthDate,
            Cpf = cpf
        };

    public void Update(string name, DateOnly? birthDate, string? cpf,
        bool canBook, bool canCancel, bool canViewHistory, bool active)
    {
        Name = name;
        BirthDate = birthDate;
        Cpf = cpf;
        CanBook = canBook;
        CanCancel = canCancel;
        CanViewHistory = canViewHistory;
        Active = active;
    }

    public void LinkUser(Guid userId)
    {
        UserId = userId;
    }

    public void Deactivate() => Active = false;
}