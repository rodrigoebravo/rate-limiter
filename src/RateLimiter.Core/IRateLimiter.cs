namespace RateLimiter.Core;

public interface IRateLimiter
{
    // Devuelve true si el request está permitido, false si fue rechazado
    bool TryAcquire();
}