namespace RateLimiter.Core;

/// <summary>
/// Immutable configuration for a rate limiter instance.
/// Use <see cref="Validate"/> to construct — it enforces invariants before the object exists.
/// </summary>
public class RateLimiterOptions
{
    /// <summary>Maximum number of tokens the limiter can hold at any time.</summary>
    public int Capacity { get; init; }

    /// <summary>Tokens added per second. Controls how fast the limiter recovers after being exhausted.</summary>
    public double RefillRatePerSecond { get; init; }

    /// <summary>
    /// Creates a validated <see cref="RateLimiterOptions"/> instance.
    /// Throws <see cref="ArgumentOutOfRangeException"/> if any parameter is zero or negative.
    /// </summary>
    public static RateLimiterOptions Validate(int capacity, double refillRatePerSecond)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0.");

        if (refillRatePerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(refillRatePerSecond), "Refill rate must be greater than 0.");

        return new RateLimiterOptions
        {
            Capacity = capacity,
            RefillRatePerSecond = refillRatePerSecond
        };
    }
}