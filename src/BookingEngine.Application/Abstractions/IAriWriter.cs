using BookingEngine.Application.Ari;

namespace BookingEngine.Application.Abstractions;

public interface IAriWriter
{
    Task UpsertAvailabilityAsync(Guid propertyId, IReadOnlyList<AvailabilityChange> changes, CancellationToken ct);

    Task UpsertRestrictionsAsync(Guid propertyId, IReadOnlyList<RestrictionChange> changes, CancellationToken ct);
}
