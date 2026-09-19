namespace BookingEngine.Application.Common;

public sealed record PageMeta(int Page, int PageSize, int Total);

public sealed record PagedResponse<T>(IReadOnlyList<T> Data, PageMeta Meta);
