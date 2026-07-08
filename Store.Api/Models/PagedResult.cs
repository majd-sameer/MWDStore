namespace Store.Api.Models;

/// <summary>
/// One page of a filtered list plus the <see cref="Total"/> count of the whole
/// filtered set, so the admin UI can render numbered pagination ("Total N").
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
