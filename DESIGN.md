# Rate Limiter — Design Document

## Problem

A rate limiter controls how many requests a client can make in a given time window.
Without one, a single client can overwhelm an API, causing degraded service for everyone else.

This implementation focuses on correctness under high concurrency — the core challenge
of any rate limiter running in a multi-threaded environment.

## Algorithm: Token Bucket

I chose Token Bucket over the other algorithms described in Alex Xu's book for three reasons:

1. **Burst handling**: it allows short bursts up to the bucket capacity, which matches
   real-world API usage patterns better than Fixed Window.
2. **Memory efficiency**: it stores only two values per client (token count + last refill timestamp),
   unlike Sliding Window Log which stores every request timestamp.
3. **Industry adoption**: it's the algorithm behind AWS API Gateway, Stripe, and most
   production rate limiters.

### How it works

Each limiter instance represents one "bucket" for one client:
- The bucket starts full (at capacity).
- Each `TryAcquire()` call consumes one token.
- Tokens are refilled continuously based on elapsed time since the last refill.
- If the bucket is empty, the request is rejected immediately (no queuing).

## Concurrency Design

This is the core challenge. Multiple threads can call `TryAcquire()` simultaneously,
so both the refill and the consume operations must be thread-safe without becoming a bottleneck.

### Token consumption — Compare-And-Swap loop

Consuming a token uses `Interlocked.CompareExchange` in a spin loop:

```csharp
var updated = current - ToMillis(1);
if (Interlocked.CompareExchange(ref _tokensMillis, updated, current) == current)
    return true;
```

This is a lock-free approach: a thread only writes if nobody else changed the value
since it last read. If two threads race, one wins and the other retries — no blocking,
no deadlocks. This is correct and fast for high-throughput scenarios.

### Refill — lock for compound operations

Refill needs to atomically update *two* variables: `_tokensMillis` and `_lastRefillTimestamp`.
`Interlocked` only handles one variable at a time, so a race between two threads could
result in double-refill. A `lock` is used here, but only for the refill path — which
is less frequent than token consumption.

The lock is also double-checked: we re-read elapsed time inside the lock because another
thread might have refilled while we were waiting, making our refill unnecessary.

### Why not lock everything?

A single `lock` around the whole method would be simpler but creates a bottleneck:
every thread blocks waiting for the lock, even for reads. The hybrid approach
(lock-free consume + locked refill) gives correctness where needed and performance where possible.

### Millitoken representation

`Interlocked` doesn't support `double`, only integer types. To avoid losing precision
when refilling fractional tokens (e.g. 0.5 tokens/sec), we store tokens multiplied by 1000
as a `long`. This means 1 token = 1000 in storage, and the math stays integer-safe.

## Trade-offs and limitations

| Decision | Trade-off |
|---|---|
| In-memory only | Fast, but state is lost on restart. A production system would use Redis for distributed rate limiting. |
| One limiter per client | The caller is responsible for maintaining a limiter per user/IP/key. A `RateLimiterService` dictionary was considered but left out to avoid overengineering for this scope. |
| No queuing | Rejected requests fail immediately. A leaky bucket variant would queue them, but adds complexity not required here. |
| Spin loop on consume | Under extreme contention (thousands of threads on one bucket) the spin loop could waste CPU. In practice, rate-limited systems don't see this pattern. |

## What I would add in production

- **Redis backend** for distributed rate limiting across multiple API instances.
- **Per-client limiter registry** (`ConcurrentDictionary<string, IRateLimiter>`) with TTL eviction.
- **HTTP middleware** (ASP.NET Core) that reads client IP or API key and applies the limiter.
- **Metrics**: counters for allowed/rejected requests per client, exposed via OpenTelemetry.
- **Circuit breaker** on the Redis client to fall back to in-memory if the store is unavailable.

## How I used AI

I used Claude as a senior pair programmer throughout this challenge:
- To validate algorithm selection and concurrency approach before writing code.
- To review the lock vs Interlocked trade-off analysis.
- To structure the test suite, particularly the concurrency tests.

Every design decision in this document I can explain and defend independently.
The millitoken trick and the double-checked refill lock were discussed and understood
before being written — not copy-pasted blindly.