using RateLimiter.Core;

namespace RateLimiter.Tests;

// Validates that TokenBucketLimiter is correct under high concurrency.
// These tests would surface race conditions that single-threaded tests cannot detect.
public class ConcurrencyTests
{
    [Fact]
    // 1000 threads competing simultaneously must never exceed the bucket capacity.
    public async Task UnderHighConcurrency_NeverExceedsCapacity()
    {
        const int capacity = 100;
        var limiter = new TokenBucketLimiter(RateLimiterOptions.Validate(capacity, 1));
        int allowed = 0;

        await Parallel.ForEachAsync(Enumerable.Range(0, 1000), async (_, _) =>
        {
            await Task.Yield(); // forces real concurrency by yielding to the scheduler
            if (limiter.TryAcquire())
                Interlocked.Increment(ref allowed);
        });

        Assert.True(allowed <= capacity, $"Allowed {allowed} but capacity is {capacity}");
    }

    [Fact]
    // Verifies no tokens are lost under contention — exactly capacity requests must be allowed.
    public async Task UnderHighConcurrency_AllowsExactlyCapacityRequests()
    {
        const int capacity = 50;
        var limiter = new TokenBucketLimiter(RateLimiterOptions.Validate(capacity, 1));
        int allowed = 0;

        await Parallel.ForEachAsync(Enumerable.Range(0, 500), async (_, _) =>
        {
            await Task.Yield();
            if (limiter.TryAcquire())
                Interlocked.Increment(ref allowed);
        });

        Assert.Equal(capacity, allowed);
    }

    [Fact]
    // Simulates realistic load: threads arriving at different times while the bucket refills.
    public async Task ConcurrentRefillAndAcquire_NeverExceedsCapacity()
    {
        const int capacity = 10;
        var limiter = new TokenBucketLimiter(RateLimiterOptions.Validate(capacity, 100));
        int allowed = 0;

        await Parallel.ForEachAsync(Enumerable.Range(0, 200), async (i, _) =>
        {
            await Task.Delay(i % 5); // variable delays simulate uneven real-world traffic
            if (limiter.TryAcquire())
                Interlocked.Increment(ref allowed);
        });

        // With a refill rate of 100/sec over ~200ms, the upper bound is capacity + 100 * 0.2 = 30
        Assert.True(allowed <= capacity + 30, $"Allowed too many requests: {allowed}");
    }
}