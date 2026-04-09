using RateLimiter.Core;

namespace RateLimiter.Tests;

// Validates correct behavior of TokenBucketLimiter under normal, single-threaded conditions.
public class TokenBucketTests
{
    [Fact]
    // Happy path: a full bucket allows exactly as many requests as its capacity.
    public void FullBucket_AllowsRequestsUpToCapacity()
    {
        var limiter = new TokenBucketLimiter(RateLimiterOptions.Validate(5, 1));

        for (int i = 0; i < 5; i++)
            Assert.True(limiter.TryAcquire(), $"Request {i + 1} should be allowed");
    }

    [Fact]
    // Once the bucket is empty, any further request must be rejected.
    public void FullBucket_RejectsRequestsBeyondCapacity()
    {
        var limiter = new TokenBucketLimiter(RateLimiterOptions.Validate(5, 1));

        for (int i = 0; i < 5; i++)
            limiter.TryAcquire();

        Assert.False(limiter.TryAcquire(), "Request beyond capacity should be rejected");
    }

    [Fact]
    // Verifies that tokens are actually replenished over time.
    public async Task AfterWaiting_NewTokensAreAvailable()
    {
        var limiter = new TokenBucketLimiter(RateLimiterOptions.Validate(1, 2));

        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire()); // bucket is now empty

        await Task.Delay(1100); // wait slightly over 1 second to guarantee at least one refill

        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    // Validate rejects a capacity of zero or less.
    public void InvalidCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RateLimiterOptions.Validate(0, 1));
    }

    [Fact]
    // Validate rejects a refill rate of zero or less.
    public void InvalidRefillRate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RateLimiterOptions.Validate(10, -1));
    }

    [Fact]
    // A disposed limiter must throw rather than silently allow or reject requests.
    public void DisposedLimiter_Throws()
    {
        var limiter = new TokenBucketLimiter(RateLimiterOptions.Validate(5, 1));
        limiter.Dispose();

        Assert.Throws<ObjectDisposedException>(() => limiter.TryAcquire());
    }
}