using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Core.Protocol
{
    /// <summary>
    /// Handles protocol negotiation between client and server
    /// </summary>
    public class ProtocolNegotiator : IProtocolNegotiator
    {
        private readonly Dictionary<string, IProtocolHandler> _handlers;
        private readonly ILogger<ProtocolNegotiator> _logger;
        private readonly ProtocolNegotiationOptions _options;

        public ProtocolNegotiator(
            IEnumerable<IProtocolHandler> handlers,
            ILogger<ProtocolNegotiator> logger,
            IOptions<ProtocolNegotiationOptions> options = null)
        {
            _handlers = handlers?.ToDictionary(h => h.ProtocolName, StringComparer.OrdinalIgnoreCase)
                       ?? throw new ArgumentNullException(nameof(handlers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new ProtocolNegotiationOptions();

            if (!_handlers.Any())
            {
                throw new ArgumentException("At least one protocol handler must be provided", nameof(handlers));
            }
        }

        /// <summary>
        /// Negotiates protocol as a server (responds to client negotiation)
        /// </summary>
        public async Task<ProtocolNegotiationResult> NegotiateAsServerAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting protocol negotiation as server");

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.NegotiationTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                // Read negotiation request from client
                var negotiationMessage = await ReadNegotiationMessageAsync(stream, linkedCts.Token);

                if (negotiationMessage == null)
                {
                    _logger.LogWarning("Failed to read negotiation message from client");
                    return CreateFailureResult("Failed to read negotiation message");
                }

                _logger.LogDebug("Received negotiation request from client. Supported protocols: {Protocols}",
                    string.Join(", ", negotiationMessage.SupportedProtocols ?? Array.Empty<string>()));

                // Select the best protocol
                var selectedProtocol = SelectBestProtocol(
                    negotiationMessage.SupportedProtocols ?? Array.Empty<string>(),
                    _options.SupportedProtocols);

                if (string.IsNullOrEmpty(selectedProtocol))
                {
                    _logger.LogWarning("No compatible protocol found. Client supports: {ClientProtocols}, Server supports: {ServerProtocols}",
                        string.Join(", ", negotiationMessage.SupportedProtocols ?? Array.Empty<string>()),
                        string.Join(", ", _options.SupportedProtocols));

                    // Send failure response
                    await SendNegotiationResponseAsync(stream, null, "No compatible protocol", linkedCts.Token);
                    return CreateFailureResult("No compatible protocol found");
                }

                _logger.LogInformation("Selected protocol: {Protocol}", selectedProtocol);

                // Send success response
                await SendNegotiationResponseAsync(stream, selectedProtocol, null, linkedCts.Token);

                return new ProtocolNegotiationResult
                {
                    IsSuccessful = true,
                    SelectedProtocol = selectedProtocol,
                    ProtocolHandler = _handlers[selectedProtocol],
                    NegotiationDuration = DateTime.UtcNow - negotiationMessage.Timestamp,
                    ClientCapabilities = negotiationMessage.Capabilities
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Protocol negotiation cancelled");
                return CreateFailureResult("Negotiation cancelled");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Protocol negotiation timed out after {Timeout} seconds", _options.NegotiationTimeoutSeconds);
                return CreateFailureResult("Negotiation timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during protocol negotiation");
                return CreateFailureResult($"Negotiation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Negotiates protocol as a client (initiates negotiation)
        /// </summary>
        public async Task<ProtocolNegotiationResult> NegotiateAsClientAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting protocol negotiation as client");

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.NegotiationTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                // Send negotiation request
                var negotiationMessage = new ProtocolNegotiationMessage
                {
                    SupportedProtocols = _options.SupportedProtocols.ToArray(),
                    PreferredProtocol = _options.SupportedProtocols.FirstOrDefault(),
                    Capabilities = GetClientCapabilities()
                };

                await SendNegotiationRequestAsync(stream, negotiationMessage, linkedCts.Token);

                // Read server response
                var response = await ReadNegotiationMessageAsync(stream, linkedCts.Token);

                if (response == null)
                {
                    _logger.LogWarning("Failed to read negotiation response from server");
                    return CreateFailureResult("Failed to read negotiation response");
                }

                if (!response.IsResponse)
                {
                    _logger.LogWarning("Received invalid negotiation message from server");
                    return CreateFailureResult("Invalid negotiation response");
                }

                if (string.IsNullOrEmpty(response.SelectedProtocol))
                {
                    _logger.LogWarning("Server rejected protocol negotiation");
                    return CreateFailureResult("Server rejected negotiation");
                }

                if (!_handlers.ContainsKey(response.SelectedProtocol))
                {
                    _logger.LogWarning("Server selected unsupported protocol: {Protocol}", response.SelectedProtocol);
                    return CreateFailureResult($"Unsupported protocol selected: {response.SelectedProtocol}");
                }

                _logger.LogInformation("Protocol negotiation successful. Selected protocol: {Protocol}", response.SelectedProtocol);

                return new ProtocolNegotiationResult
                {
                    IsSuccessful = true,
                    SelectedProtocol = response.SelectedProtocol,
                    ProtocolHandler = _handlers[response.SelectedProtocol],
                    NegotiationDuration = DateTime.UtcNow - negotiationMessage.Timestamp,
                    ServerCapabilities = response.Capabilities
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Protocol negotiation cancelled");
                return CreateFailureResult("Negotiation cancelled");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Protocol negotiation timed out after {Timeout} seconds", _options.NegotiationTimeoutSeconds);
                return CreateFailureResult("Negotiation timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during protocol negotiation");
                return CreateFailureResult($"Negotiation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects protocol from stream without negotiation
        /// </summary>
        public async Task<IProtocolHandler> DetectProtocolAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            foreach (var handler in _handlers.Values.OrderBy(h => h.ProtocolName))
            {
                try
                {
                    var originalPosition = stream.Position;
                    var canHandle = await handler.CanHandleAsync(stream, cancellationToken);
                    stream.Position = originalPosition; // Reset position

                    if (canHandle)
                    {
                        _logger.LogDebug("Detected protocol: {Protocol}", handler.ProtocolName);
                        return handler;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Protocol {Protocol} cannot handle stream", handler.ProtocolName);
                    // Continue to next handler
                }
            }

            _logger.LogWarning("No protocol handler could handle the stream");
            return null;
        }

        private string SelectBestProtocol(string[] clientProtocols, IList<string> serverProtocols)
        {
            // Find the first server protocol that the client also supports
            foreach (var serverProtocol in serverProtocols)
            {
                if (clientProtocols.Contains(serverProtocol, StringComparer.OrdinalIgnoreCase))
                {
                    return serverProtocol;
                }
            }

            return null;
        }

        private async Task<ProtocolNegotiationMessage> ReadNegotiationMessageAsync(Stream stream, CancellationToken cancellationToken)
        {
            // For simplicity, assume JSON-RPC format for negotiation
            // In a real implementation, you might use a simpler format
            var jsonHandler = _handlers.Values.FirstOrDefault(h => h.ProtocolName.Equals("json-rpc", StringComparison.OrdinalIgnoreCase));
            if (jsonHandler == null)
            {
                throw new InvalidOperationException("JSON-RPC handler required for negotiation");
            }

            var message = await jsonHandler.ReadMessageAsync(stream, cancellationToken);
            return message as ProtocolNegotiationMessage;
        }

        private async Task SendNegotiationRequestAsync(Stream stream, ProtocolNegotiationMessage message, CancellationToken cancellationToken)
        {
            var jsonHandler = _handlers.Values.FirstOrDefault(h => h.ProtocolName.Equals("json-rpc", StringComparison.OrdinalIgnoreCase));
            if (jsonHandler == null)
            {
                throw new InvalidOperationException("JSON-RPC handler required for negotiation");
            }

            await jsonHandler.WriteMessageAsync(stream, message, cancellationToken);
        }

        private async Task SendNegotiationResponseAsync(Stream stream, string selectedProtocol, string errorMessage, CancellationToken cancellationToken)
        {
            var response = new ProtocolNegotiationMessage
            {
                IsResponse = true,
                SelectedProtocol = selectedProtocol,
                Capabilities = GetServerCapabilities()
            };

            if (!string.IsNullOrEmpty(errorMessage))
            {
                response.Metadata["error"] = errorMessage;
            }

            await SendNegotiationRequestAsync(stream, response, cancellationToken);
        }

        private Dictionary<string, object> GetClientCapabilities()
        {
            return new Dictionary<string, object>
            {
                ["streaming"] = _handlers.Values.Any(h => h.SupportsStreaming),
                ["binary"] = _handlers.Values.Any(h => h.IsBinary),
                ["protocols"] = _handlers.Keys.ToArray()
            };
        }

        private Dictionary<string, object> GetServerCapabilities()
        {
            return GetClientCapabilities(); // Same for now
        }

        private ProtocolNegotiationResult CreateFailureResult(string errorMessage)
        {
            return new ProtocolNegotiationResult
            {
                IsSuccessful = false,
                ErrorMessage = errorMessage,
                SelectedProtocol = _options.DefaultProtocol,
                ProtocolHandler = _handlers.ContainsKey(_options.DefaultProtocol) ? _handlers[_options.DefaultProtocol] : null
            };
        }
    }

    /// <summary>
    /// Interface for protocol negotiation
    /// </summary>
    public interface IProtocolNegotiator
    {
        Task<ProtocolNegotiationResult> NegotiateAsServerAsync(Stream stream, CancellationToken cancellationToken = default);
        Task<ProtocolNegotiationResult> NegotiateAsClientAsync(Stream stream, CancellationToken cancellationToken = default);
        Task<IProtocolHandler> DetectProtocolAsync(Stream stream, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of protocol negotiation
    /// </summary>
    public class ProtocolNegotiationResult
    {
        public bool IsSuccessful { get; set; }
        public string SelectedProtocol { get; set; }
        public IProtocolHandler ProtocolHandler { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan NegotiationDuration { get; set; }
        public Dictionary<string, object> ClientCapabilities { get; set; }
        public Dictionary<string, object> ServerCapabilities { get; set; }
    }

    /// <summary>
    /// Protocol negotiation options
    /// </summary>
    public class ProtocolNegotiationOptions
    {
        /// <summary>
        /// Whether to enable protocol negotiation
        /// </summary>
        public bool EnableNegotiation { get; set; } = true;

        /// <summary>
        /// Supported protocols in order of preference
        /// </summary>
        public List<string> SupportedProtocols { get; set; } = new List<string>
        {
            "json-rpc",
            "msgpack",
            "grpc"
        };

        /// <summary>
        /// Default protocol if negotiation fails
        /// </summary>
        public string DefaultProtocol { get; set; } = "json-rpc";

        /// <summary>
        /// Negotiation timeout in seconds
        /// </summary>
        public int NegotiationTimeoutSeconds { get; set; } = 5;
    }
}
