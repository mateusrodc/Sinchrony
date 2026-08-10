using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Services;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("classes/{classId}/waitlist")]
[Produces("application/json")]
public class WaitlistController(
    IClassRepository classRepository,
    IWaitlistRepository waitlistRepository,
    IBookingRepository bookingRepository,
    IStudentPackageRepository studentPackageRepository,
    ISettingsRepository settingsRepository) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpPost]
    public async Task<IActionResult> Join(Guid classId, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(classId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        if (@class.Status != Domain.Enums.ClassStatus.scheduled)
            throw DomainException.Conflict("CLASS_UNAVAILABLE",
                "Esta aula não está disponível.");

        // Verifica se ainda há vaga (não deveria entrar na fila se houver vaga)
        var activeCount = await classRepository.CountActiveBookingsWithLockAsync(classId, ct);
        if (activeCount < @class.TotalSpots)
            throw DomainException.Conflict("CLASS_HAS_SPOTS",
                "Esta aula ainda tem vagas disponíveis. Faça uma reserva normal.");

        // Verifica se AllowWaitlist está ativo na cascata
        var studentPackage = await studentPackageRepository.GetActiveByStudentAsync(UserId, ct);
        var settings = await settingsRepository.GetAsync(ct);
        var allowWaitlist = settings is not null &&
            PackageRuleResolver.GetAllowWaitlist(studentPackage, settings);

        if (!allowWaitlist)
            throw DomainException.Validation("WAITLIST_NOT_ALLOWED",
                "Lista de espera não está habilitada para esta aula.");

        // Verifica se aluno já tem reserva
        var alreadyBooked = await bookingRepository.HasActiveBookingAsync(UserId, classId, ct);
        if (alreadyBooked)
            throw DomainException.Conflict("ALREADY_BOOKED",
                "Você já tem uma reserva nesta aula.");

        // Verifica se já está na fila
        var existing = await waitlistRepository.GetByClassAndStudentAsync(classId, UserId, ct);
        if (existing is not null)
            throw DomainException.Conflict("ALREADY_IN_WAITLIST",
                "Você já está na lista de espera desta aula.");

        var position = await waitlistRepository.CountByClassAsync(classId, ct) + 1;
        var entry = WaitlistEntry.Create(classId, UserId, position);
        await waitlistRepository.AddAsync(entry, ct);
        await waitlistRepository.SaveAsync(ct);

        return StatusCode(201, new
        {
            id = entry.Id,
            classId = entry.ClassId,
            studentId = entry.StudentId,
            position = entry.Position,
            enteredAt = entry.EnteredAt,
            status = entry.Status
        });
    }

    [HttpDelete]
    public async Task<IActionResult> Leave(Guid classId, CancellationToken ct)
    {
        var entry = await waitlistRepository.GetByClassAndStudentAsync(classId, UserId, ct)
            ?? throw DomainException.NotFound("Você não está na lista de espera desta aula.");

        entry.MarkExpired(); // usa como "saiu da fila voluntariamente"
        await waitlistRepository.SaveAsync(ct);

        return Ok(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid classId, CancellationToken ct)
    {
        var entries = await waitlistRepository.ListByClassAsync(classId, ct);
        return Ok(new
        {
            data = entries.Select(e => new
            {
                id = e.Id,
                studentId = e.StudentId,
                studentName = e.Student?.Name,
                position = e.Position,
                enteredAt = e.EnteredAt,
                status = e.Status
            })
        });
    }
}