using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;

namespace Sinchrony.Domain.Entities;

public class Class
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public Guid ClassTypeId { get; private set; }
    public Guid TeacherId { get; private set; }
    public Guid StudioId { get; private set; }
    public DateOnly Date { get; private set; }
    public string StartTime { get; private set; } = string.Empty;
    public string EndTime { get; private set; } = string.Empty;
    public int Duration { get; private set; }
    public int TotalSpots { get; private set; }
    public ClassStatus Status { get; private set; } = ClassStatus.scheduled;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public ClassType? ClassType { get; private set; }
    public User? Teacher { get; private set; }
    public Studio? Studio { get; private set; }
    public ICollection<Booking> Bookings { get; private set; } = [];
    public ICollection<ClassSession> Sessions { get; private set; } = [];

    protected Class() { }

    public static Class Create(string name, Guid classTypeId, Guid teacherId, Guid studioId,
        DateOnly date, string startTime, string endTime, int duration, int totalSpots)
        => new()
        {
            Name = name,
            ClassTypeId = classTypeId,
            TeacherId = teacherId,
            StudioId = studioId,
            Date = date,
            StartTime = startTime,
            EndTime = endTime,
            Duration = duration,
            TotalSpots = totalSpots
        };

    public void Update(string name, Guid classTypeId, Guid teacherId, Guid studioId,
        DateOnly date, string startTime, string endTime, int duration, int totalSpots, ClassStatus status)
    {
        Name = name; ClassTypeId = classTypeId; TeacherId = teacherId; StudioId = studioId;
        Date = date; StartTime = startTime; EndTime = endTime;
        Duration = duration; TotalSpots = totalSpots; Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Start() { Status = ClassStatus.in_progress; UpdatedAt = DateTime.UtcNow; }
    public void Complete() { Status = ClassStatus.completed; UpdatedAt = DateTime.UtcNow; }
    public void Cancel() { Status = ClassStatus.cancelled; UpdatedAt = DateTime.UtcNow; }

    // Reservas ativas = tudo que não foi cancelado (confirmed, attended, no_show, waitlisted).
    // Depende de Bookings estar carregado — ClassRepository.GetByIdAsync já traz só as não canceladas.
    public int ActiveBookingsCount => Bookings.Count(b => b.Status != BookingStatus.cancelled);

    // Cancelar aula com reservas exigiria devolver crédito e avisar aluno/fila; esse fluxo não existe,
    // então só é permitido cancelar aula sem reservas ativas.
    public void EnsureNoActiveBookings()
    {
        var active = ActiveBookingsCount;
        if (active > 0)
            throw DomainException.Conflict("CLASS_HAS_BOOKINGS",
                $"A aula possui {active} reserva(s) ativa(s). Cancele as reservas antes de desativar.");
    }

    // "Desativar" aula = status cancelled (reserva/fila só são aceitas em aula scheduled).
    public void Deactivate()
    {
        if (Status != ClassStatus.scheduled)
            throw DomainException.Conflict("CLASS_NOT_SCHEDULED", "Só é possível desativar uma aula agendada.");
        EnsureNoActiveBookings();
        Cancel();
    }

    public void Reactivate(DateOnly today)
    {
        if (Status != ClassStatus.cancelled)
            throw DomainException.Conflict("CLASS_NOT_CANCELLED", "Só é possível reativar uma aula desativada.");
        if (Date < today)
            throw DomainException.Conflict("CLASS_IN_PAST", "Não é possível reativar uma aula com data passada.");
        Status = ClassStatus.scheduled;
        UpdatedAt = DateTime.UtcNow;
    }
}
