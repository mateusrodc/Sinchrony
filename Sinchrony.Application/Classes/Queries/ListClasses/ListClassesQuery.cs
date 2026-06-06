using MediatR;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Queries.ListClasses;

public record ListClassesQuery(DateOnly? Date, string? Type, Guid? StudioId) : IRequest<IEnumerable<ClassDto>>;

public record ClassDto(
    Guid Id,
    string Name,
    string Type,
    Guid ClassTypeId,
    string Instructor,
    string? InstructorAvatar,
    Guid TeacherId,
    string Date,
    string StartTime,
    string EndTime,
    int Duration,
    int TotalSpots,
    int AvailableSpots,
    int EnrolledCount,
    string Status,
    StudioDto Studio);

public record StudioDto(
    Guid Id,
    string Name,
    string Address,
    int Capacity,
    string OpeningTime,
    string ClosingTime);

public class ListClassesQueryHandler(IClassRepository classRepository)
    : IRequestHandler<ListClassesQuery, IEnumerable<ClassDto>>
{
    public async Task<IEnumerable<ClassDto>> Handle(ListClassesQuery request, CancellationToken ct)
    {
        var classes = await classRepository.ListAsync(request.Date, request.Type, request.StudioId, ct);
        return classes.Select(MapToDto);
    }

    public static ClassDto MapToDto(Domain.Entities.Class c)
    {
        var enrolled = c.Bookings.Count(b => b.Status != Domain.Enums.BookingStatus.cancelled);
        return new ClassDto(
            c.Id, c.Name,
            c.ClassType?.Name ?? string.Empty,
            c.ClassTypeId,
            c.Teacher?.Name ?? string.Empty,
            c.Teacher?.Avatar,
            c.TeacherId,
            c.Date.ToString("yyyy-MM-dd"),
            c.StartTime, c.EndTime, c.Duration,
            c.TotalSpots,
            c.TotalSpots - enrolled,
            enrolled,
            c.Status.ToString(),
            new StudioDto(
                c.Studio!.Id, c.Studio.Name, c.Studio.Address,
                c.Studio.Capacity, c.Studio.OpeningTime, c.Studio.ClosingTime));
    }
}