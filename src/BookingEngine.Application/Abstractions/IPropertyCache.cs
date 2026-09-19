namespace BookingEngine.Application.Abstractions;

/// <summary>Read cache scoped to a property; any write to the property's data must call <see cref="InvalidateAsync"/>.</summary>
public interface IPropertyCache
{
    Task<T> GetOrCreateAsync<T>(Guid propertyId, string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct);

    Task InvalidateAsync(Guid propertyId, CancellationToken ct);
}
