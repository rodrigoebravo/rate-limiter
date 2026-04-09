namespace RateLimiter.Core;

/// <summary>
/// Thread-safe rate limiter using the Token Bucket algorithm.
/// Tokens accumulate over time up to a fixed capacity. When no tokens remain, requests are rejected immediately.
/// Designed for single-process use — distributed environments require an external store (e.g. Redis).
/// </summary>
public sealed class TokenBucketLimiter : IRateLimiter, IDisposable
{
    private readonly RateLimiterOptions _options;

    // Stored as integer milliseconds (value × 1000) because Interlocked does not support double.
    private long _tokensMillis;

    // UTC ticks captured on each refill to calculate elapsed time.
    private long _lastRefillTimestamp;

    // Guards the refill path — updating two variables atomically requires a lock.
    private readonly object _refillLock = new();

    private bool _disposed;

    /// <param name="options">Validated configuration. Use <see cref="RateLimiterOptions.Validate"/>.</param>
    public TokenBucketLimiter(RateLimiterOptions options)
    {
        _options = options;
        _tokensMillis = ToMillis(options.Capacity); // starts full
        _lastRefillTimestamp = DateTime.UtcNow.Ticks;
    }

    /// <summary>
    /// Attempts to acquire one token.
    /// Returns <c>true</c> if the request is allowed, <c>false</c> if the limit is exceeded.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Refill();
        return TryConsumeToken();
    }

    // Adds tokens based on time elapsed since the last refill, capped at capacity.
    private void Refill()
    {
        var now = DateTime.UtcNow.Ticks;
        var elapsed = TimeSpan.FromTicks(now - Interlocked.Read(ref _lastRefillTimestamp)).TotalSeconds;

        if (elapsed * _options.RefillRatePerSecond < 1) return; // not enough time to add a token, skip the lock

        lock (_refillLock)
        {
            // Re-read inside the lock — another thread may have refilled while we waited
            var elapsedInLock = TimeSpan.FromTicks(now - Interlocked.Read(ref _lastRefillTimestamp)).TotalSeconds;
            var toAddInLock = elapsedInLock * _options.RefillRatePerSecond;

            if (toAddInLock < 1) return;

            var refilled = Math.Min(
                Interlocked.Read(ref _tokensMillis) + ToMillis(toAddInLock),
                ToMillis(_options.Capacity));

            Interlocked.Exchange(ref _tokensMillis, refilled);
            Interlocked.Exchange(ref _lastRefillTimestamp, now);
        }
    }

    // Consumes one token using a Compare-And-Swap loop (lock-free).
    // Retries if another thread modified _tokensMillis between our read and write.
    private bool TryConsumeToken()
    {
        while (true)
        {
            var current = Interlocked.Read(ref _tokensMillis);

            if (current < ToMillis(1)) return false; // no tokens available

            var updated = current - ToMillis(1);

            // Writes only if _tokensMillis still equals current — otherwise retries
            if (Interlocked.CompareExchange(ref _tokensMillis, updated, current) == current)
                return true;
        }
    }

    // Converts tokens to the internal millitoken representation.
    private static long ToMillis(double tokens) => (long)(tokens * 1000);

    public void Dispose() => _disposed = true;
}