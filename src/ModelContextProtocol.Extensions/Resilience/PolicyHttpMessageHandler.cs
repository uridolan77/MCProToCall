using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// HTTP message handler that applies a Polly policy to HTTP requests
    /// </summary>
    public class PolicyHttpMessageHandler : DelegatingHandler
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;

        /// <summary>
        /// Initializes a new instance of the PolicyHttpMessageHandler class
        /// </summary>
        /// <param name="policy">The policy to apply</param>
        public PolicyHttpMessageHandler(IAsyncPolicy<HttpResponseMessage> policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        /// <summary>
        /// Sends an HTTP request with the policy applied
        /// </summary>
        /// <param name="request">The HTTP request message</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The HTTP response message</returns>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Create a context with the request
            var context = new Context($"{request.Method}:{request.RequestUri}");
            
            // Execute the request with the policy
            return await _policy.ExecuteAsync(
                (ctx, ct) => base.SendAsync(request, ct),
                context,
                cancellationToken);
        }
    }
}
