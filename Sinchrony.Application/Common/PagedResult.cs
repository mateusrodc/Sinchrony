namespace Sinchrony.Application.Common;

public record PaginationMeta(int Page, int PageSize, int Total, int TotalPages);

public record PagedResult<T>(IEnumerable<T> Data, PaginationMeta Pagination);

public static class PagedResult
{
    public static PagedResult<T> Create<T>(IEnumerable<T> data, int page, int pageSize, int total)
    {
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PagedResult<T>(data, new PaginationMeta(page, pageSize, total, totalPages));
    }
}