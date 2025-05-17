using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace ModelContextProtocol.Core.Tests.Models
{
    public class JsonRpcTests
    {
        [Fact]
        public void JsonRpcRequest_ShouldSerializeAndDeserialize_Correctly()
        {
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = "1",
                Method = "test.method",
                Params = JsonDocument.Parse("{\"param1\":\"value1\"}").RootElement
            };

            var json = JsonSerializer.Serialize(request);
            var deserializedRequest = JsonSerializer.Deserialize<JsonRpcRequest>(json);

            Assert.Equal(request.JsonRpc, deserializedRequest.JsonRpc);
            Assert.Equal(request.Id, deserializedRequest.Id);
            Assert.Equal(request.Method, deserializedRequest.Method);
            Assert.Equal(request.Params.ToString(), deserializedRequest.Params.ToString());
        }

        [Fact]
        public void JsonRpcResponse_ShouldSerializeAndDeserialize_Correctly()
        {
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = "1",
                Result = new { success = true }
            };

            var json = JsonSerializer.Serialize(response);
            var deserializedResponse = JsonSerializer.Deserialize<JsonRpcResponse>(json);

            Assert.Equal(response.JsonRpc, deserializedResponse.JsonRpc);
            Assert.Equal(response.Id, deserializedResponse.Id);
            Assert.Equal(JsonSerializer.Serialize(response.Result), JsonSerializer.Serialize(deserializedResponse.Result));
        }

        [Fact]
        public void JsonRpcError_ShouldSerializeAndDeserialize_Correctly()
        {
            var error = new JsonRpcError
            {
                Code = -32601,
                Message = "Method not found",
                Data = null
            };

            var json = JsonSerializer.Serialize(error);
            var deserializedError = JsonSerializer.Deserialize<JsonRpcError>(json);

            Assert.Equal(error.Code, deserializedError.Code);
            Assert.Equal(error.Message, deserializedError.Message);
            Assert.Equal(error.Data, deserializedError.Data);
        }
    }
}