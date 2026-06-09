namespace Sinchrony.Application.Common;

public record PagedResult<T>(
    IEnumerable<T> Data,
    int Page,
    int PageSize,
    int Total,
    int TotalPages);