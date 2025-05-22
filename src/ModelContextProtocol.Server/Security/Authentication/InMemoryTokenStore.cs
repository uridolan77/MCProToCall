using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ModelContextProtocol.Server.Security.Authentication
{
    /// <summary>
    /// In-memory token store implementation
    /// </summary>
    public class InMemoryTokenStore : ITokenStore
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _tokens;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryTokenStore"/> class
        /// </summary>
        public InMemoryTokenStore()
        {
            _tokens = new ConcurrentDictionary<string, ConcurrentDictionary<string, bool>>();
        }

        /// <inheritdoc/>
        public Task StoreTokenAsync(string username, string token)
        {
            var userTokens = _tokens.GetOrAdd(username, _ => new ConcurrentDictionary<string, bool>());
            userTokens[token] = true;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<bool> IsTokenValidAsync(string username, string token)
        {
            if (_tokens.TryGetValue(username, out var userTokens))
            {
                return Task.FromResult(userTokens.TryGetValue(token, out var isValid) && isValid);
            }
            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task RevokeTokenAsync(string username, string token)
        {
            if (_tokens.TryGetValue(username, out var userTokens))
            {
                userTokens.TryRemove(token, out _);
            }
            return Task.CompletedTask;
        }
    }
}
