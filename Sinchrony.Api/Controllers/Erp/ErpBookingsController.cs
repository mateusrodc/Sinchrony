using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
public class ErpBookingsController(
    IBookingRepository bookingRepository,
    IWaitlistPromotionService waitlistPromotionService,
    INoShowPenaltyService noShowPenaltyService,
    IAttendanceRepository attendanceRepository) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ErpBookingListResponseExample))]
    public async Task<IActionResult> List(
    [FromQuery] Guid? classId,
    [FromQuery] Guid? studentId,
    [FromQuery] string? status,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
    {
        var (items, total) = await bookingRepository.ListErpPagedAsync(
    classId, studentId, status, page, pageSize, ct);

        var data = items.Select(b => new
        {
            id = b.Id,
            classId = b.ClassId,
            className = b.Class?.Name,
            studentId = b.StudentId,
            studentName = b.Student?.Name,
            studentEmail = b.Student?.Email,
            studentAvatar = b.Student?.Avatar,
            studentPhone = b.Student?.Phone,
            status = b.Status.ToString(),
            bikeNumber = b.BikeNumber,
            bookedAt = b.BookedAt,
            checkedIn = b.CheckedIn
        });

        return Ok(PagedResult.Create(data, page, pageSize, total));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ErpBookingDetailResponseExample))]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Booking not found.");
        return Ok(new
        {
            id = booking.Id,
            classId = booking.ClassId,
            studentId = booking.StudentId,
            status = booking.Status.ToString(),
            bikeNumber = booking.BikeNumber,
            bookedAt = booking.BookedAt,
            checkedIn = booking.CheckedIn
        });
    }

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Booking not found.");

        if (booking.Status == Domain.Enums.BookingStatus.attended)
            throw DomainException.Validation("ALREADY_ATTENDED", "Cannot cancel attended booking.");

        booking.Cancel();
        await bookingRepository.SaveAsync(ct);

        // Libera a vaga pro próximo da lista de espera, se houver — antes só o cancelamento
        // feito pelo próprio aluno no App disparava isso (Cláusula 8.2/8.3 do Termo).
        await waitlistPromotionService.PromoteNextAsync(booking.ClassId, booking.Class?.Name ?? "sua aula", ct);

        return Ok(new { id = booking.Id, status = booking.Status.ToString() });
    }

    [HttpPatch("{id}/no-show")]
    public async Task<IActionResult> NoShow(Guid id, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Booking not found.");

        if (booking.Status != Domain.Enums.BookingStatus.confirmed)
            throw DomainException.Validation("INVALID_STATUS", "Only confirmed bookings can be marked as no-show.");

        booking.MarkNoShow();

        // Sincroniza o AttendanceRecord e registra quem marcou a falta (auditoria) — antes só
        // o Booking era tocado, deixando o registro de presença desalinhado e sem rastro de
        // quem fez a ação.
        var attendance = await attendanceRepository.GetByBookingAsync(booking.Id, ct);
        attendance?.UpdateStatus("no_show", UserId);
        await attendanceRepository.SaveAsync(ct);

        await bookingRepository.SaveAsync(ct);

        // Devolve o crédito se o pacote do aluno tiver NoShowCreditPenalty = false.
        await noShowPenaltyService.ApplyAsync(booking.StudentId, ct);

        // Idem: falta marcada manualmente pela equipe também libera a vaga pra fila.
        await waitlistPromotionService.PromoteNextAsync(booking.ClassId, booking.Class?.Name ?? "sua aula", ct);

        return Ok(new { id = booking.Id, status = booking.Status.ToString() });
    }
}