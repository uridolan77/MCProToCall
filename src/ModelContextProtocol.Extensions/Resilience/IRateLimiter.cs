using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// Interface for rate limiters
    /// </summary>
    public interface IRateLimiter
    {
        /// <summary>
        /// Tries to acquire a permit from the rate limiter
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if a permit was acquired, false otherwise</returns>
        Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Acquires a permit from the rate limiter, waiting if necessary
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task that completes when a permit is acquired</returns>
        Task AcquireAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current number of available permits
        /// </summary>
        int AvailablePermits { get; }

        /// <summary>
        /// Gets the maximum number of permits
        /// </summary>
        int MaxPermits { get; }

        /// <summary>
        /// Gets the current rate limit in permits per second
        /// </summary>
        double PermitsPerSecond { get; }
    }
}
