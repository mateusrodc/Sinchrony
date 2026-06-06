using MediatR;
using Sinchrony.Application.Classes.Queries.ListClasses;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Queries.ListClassesToday;

public record ListClassesTodayQuery : IRequest<IEnumerable<ClassDto>>;

public class ListClassesTodayQueryHandler(IClassRepository classRepository)
    : IRequestHandler<ListClassesTodayQuery, IEnumerable<ClassDto>>
{
    public async Task<IEnumerable<ClassDto>> Handle(ListClassesTodayQuery request, CancellationToken ct)
    {
        var classes = await classRepository.ListTodayAsync(ct);
        return classes.Select(ListClassesQueryHandler.MapToDto);
    }
}