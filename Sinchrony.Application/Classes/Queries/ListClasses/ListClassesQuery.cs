using MediatR;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Queries.ListClasses;

public record ListClassesQuery(
    DateOnly? Date, string? Type, Guid? StudioId,
    int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ClassDto>>;

public record ClassDto(
    Guid Id,
    string Name,
    string Type,
    bool UsesBikes,
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
    string ClosingTime,
    Guid? UnitId);

public class ListClassesQueryHandler(IClassRepository classRepository)
    : IRequestHandler<ListClassesQuery, PagedResult<ClassDto>>
{
    public async Task<PagedResult<ClassDto>> Handle(ListClassesQuery request, CancellationToken ct)
    {
        var (items, total) = await classRepository.ListPagedAsync(
            request.Date, request.Type, request.StudioId,
            request.Page, request.PageSize, ct);

        return PagedResult.Create(items.Select(MapToDto), request.Page, request.PageSize, total);
    }

    public static ClassDto MapToDto(Domain.Entities.Class c)
    {
        var enrolled = c.Bookings.Count(b => b.Status != Domain.Enums.BookingStatus.cancelled);
        return new ClassDto(
            c.Id, c.Name,
            c.ClassType?.Name ?? string.Empty,
            c.ClassType?.UsesBikes ?? false,
            c.ClassTypeId,
            c.Teacher?.Name ?? string.Empty,
            c.Teacher?.Avatar,
            c.TeacherId,
            c.Date.ToString("yyyy-MM-dd"),
            c.StartTime, c.EndTime, c.Duration,
            c.TotalSpots, c.TotalSpots - enrolled, enrolled,
            c.Status.ToString(),
            new StudioDto(
                c.Studio!.Id, c.Studio.Name, c.Studio.Address,
                c.Studio.Capacity, c.Studio.OpeningTime, c.Studio.ClosingTime, c.Studio.UnitId));
    }
}