using System;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Core.Interfaces
{
    public interface IMcpServer
    {
        Task StartAsync();
        Task StopAsync();
        void RegisterMethod(string methodName, Func<JsonElement, Task<object>> handler);
        Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request);
    }
}