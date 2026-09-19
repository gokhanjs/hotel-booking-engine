namespace BookingEngine.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Touch(DateTimeOffset now)
    {
        if (CreatedAt == default)
        {
            CreatedAt = now;
        }

        UpdatedAt = now;
    }
}
