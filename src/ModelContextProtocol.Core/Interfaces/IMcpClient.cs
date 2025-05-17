using System.Threading.Tasks;
using ModelContextProtocol.Core.Models.Mcp;

namespace ModelContextProtocol.Core.Interfaces
{
    public interface IMcpClient
    {
        Task ConnectAsync();
        Task DisconnectAsync();
        Task<TResult> CallMethodAsync<TResult>(string method, object parameters = null);
        Task<McpCapabilities> GetCapabilitiesAsync();
        Task<TResult> GetResourceAsync<TResult>(string resourceId);
        Task<TResult> ExecuteToolAsync<TResult>(string toolId, object input);
        Task<string> RenderPromptAsync(string promptId, object variables);
        Task SendMessageAsync(string message);
        Task<string> ReceiveMessageAsync();
    }
}