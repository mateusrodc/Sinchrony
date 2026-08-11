using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Bookings.Commands.CreateBooking;
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
    ISettingsRepository settingsRepository,
    IMediator mediator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    // Não existe job agendado no projeto pra vencer a janela de 5 min sozinho (ver
    // DEMANDA_LISTA_ESPERA_TOLERANCIA_NOSHOW_PACOTES_BACKEND.md, item 2, mesma lacuna de
    // infraestrutura). Como paliativo, toda vez que alguém interage com a lista de espera
    // dessa aula (entra, consulta ou tenta confirmar), primeiro reconcilia uma notificação
    // vencida sem resposta e promove o próximo — cobre o caso comum (app faz polling/refresh),
    // mas não é um cron de verdade. Sem side effect se não houver nada vencido.
    private async Task ExpireAndPromoteAsync(Guid classId, CancellationToken ct)
    {
        var current = await waitlistRepository.GetCurrentNotifiedAsync(classId, ct);
        if (current is null || current.ExpiresAt is null || current.ExpiresAt >= DateTime.UtcNow)
            return;

        current.MarkExpired();

        var next = await waitlistRepository.GetNextWaitingAsync(classId, ct);
        next?.Notify();

        await waitlistRepository.SaveAsync(ct);
    }

    [HttpPost]
    public async Task<IActionResult> Join(Guid classId, CancellationToken ct)
    {
        await ExpireAndPromoteAsync(classId, ct);

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

    [HttpPost("claim")]
    public async Task<IActionResult> Claim(Guid classId, CancellationToken ct)
    {
        await ExpireAndPromoteAsync(classId, ct);

        var entry = await waitlistRepository.GetByClassAndStudentAsync(classId, UserId, ct)
            ?? throw DomainException.NotFound("Você não está na lista de espera desta aula.");

        if (entry.Status != "notified")
            throw DomainException.Conflict("NOT_NOTIFIED",
                "Você ainda não foi chamado para esta vaga.");

        if (entry.ExpiresAt is null || entry.ExpiresAt < DateTime.UtcNow)
        {
            entry.MarkExpired();
            await waitlistRepository.SaveAsync(ct);
            throw DomainException.Conflict("WAITLIST_CLAIM_EXPIRED",
                "O prazo de 5 minutos para confirmar a vaga expirou.");
        }

        // Reaproveita a criação de reserva normal (débito de crédito, checagem de capacidade,
        // conflito de horário etc.) em vez de duplicar essa lógica aqui.
        var booking = await mediator.Send(
            new CreateBookingCommand(UserId, UserId, classId, null), ct);

        entry.MarkConverted();
        await waitlistRepository.SaveAsync(ct);

        return StatusCode(201, booking);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid classId, CancellationToken ct)
    {
        await ExpireAndPromoteAsync(classId, ct);

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