namespace BookingEngine.Domain.Common;

internal static class Guard
{
    public static string NotBlank(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static int AtLeast(int value, int min, string name) =>
        value >= min ? value : throw new DomainException($"{name} must be at least {min}.");

    public static decimal NotNegative(decimal value, string name) =>
        value >= 0 ? value : throw new DomainException($"{name} must not be negative.");
}
