# Rate Limiter

A thread-safe Token Bucket rate limiter implemented in C# / .NET 10.

Built as part of a system design coding challenge based on Alex Xu's
*System Design Interview* (Vol. 1), Chapter 4.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Run the tests

```bash
dotnet test
```

Expected output:
total: 9; failed: 0; succeeded: 9

## Usage

```csharp
// Create a limiter: 100 tokens capacity, refills 10 tokens per second
var options = RateLimiterOptions.Validate(capacity: 100, refillRatePerSecond: 10);
var limiter = new TokenBucketLimiter(options);

// On each incoming request:
if (limiter.TryAcquire())
{
    // process request
}
else
{
    // return HTTP 429 Too Many Requests
}
```

## Project structure
/src/RateLimiter.Core      — core algorithm and interfaces
/tests/RateLimiter.Tests   — unit tests and concurrency tests
DESIGN.md                  — architectural decisions and trade-offs

## Design

See [DESIGN.md](./DESIGN.md) for algorithm choice, concurrency decisions, and trade-offs.