using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Queries.GetClassStudents;

public record GetClassStudentsQuery(Guid ClassId) : IRequest<IEnumerable<ClassStudentDto>>;

public record ClassStudentDto(
    Guid Id, string Name, string Email,
    string? Avatar,
    string? Phone,
    int? BikeNumber, string Status);

public class GetClassStudentsQueryHandler(IBookingRepository bookingRepository, IClassRepository classRepository)
    : IRequestHandler<GetClassStudentsQuery, IEnumerable<ClassStudentDto>>
{
    public async Task<IEnumerable<ClassStudentDto>> Handle(GetClassStudentsQuery request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var bookings = await bookingRepository.ListErpAsync(request.ClassId, null, null, ct);

        return bookings
            .Where(b => b.Status != Domain.Enums.BookingStatus.cancelled)
            .Select(b => new ClassStudentDto(
                b.Student!.Id,
                b.Student.Name,
                b.Student.Email,
                b.Student.Avatar,
                b.Student.Phone,
                b.BikeNumber,
                b.Status.ToString()));
    }
}