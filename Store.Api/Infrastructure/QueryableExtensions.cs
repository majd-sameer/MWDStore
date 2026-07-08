using Microsoft.EntityFrameworkCore;
using Store.Api.Models;

namespace Store.Api.Infrastructure;

/// <summary>
/// Paging helpers that fold the copy-pasted clamp + <c>CountAsync</c> +
/// <c>Skip/Take</c> boilerplate into a single call producing a
/// <see cref="PagedResult{T}"/>. Count runs on the filtered (pre-paged) query.
/// </summary>
public static class QueryableExtensions
{
    private const int MaxPageSize = 200;

    /// <summary>Page a query and project each row to a DTO (post-materialisation map).</summary>
    public static async Task<PagedResult<TResult>> ToPagedResultAsync<TSource, TResult>(
        this IQueryable<TSource> query,
        int page,
        int pageSize,
        Func<TSource, TResult> map,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Normalize(page, pageSize);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<TResult>(rows.Select(map).ToList(), total, page, pageSize);
    }

    /// <summary>Page a query that already projects to its result shape (e.g. an EF <c>Select</c>).</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Normalize(page, pageSize);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<T>(items, total, page, pageSize);
    }

    private static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize));
}
