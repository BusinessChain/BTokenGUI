using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BTokenGUI.Models;

internal static class Node
{
  const string URL_RPC = "http://127.0.0.1:8778/";

  static HttpClient HttpClient = new();


  internal static async Task<JsonElement> SendRequest(string method, object parameters)
  {
    byte[] request = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
    {
      ["jsonrpc"] = "2.0",
      ["id"] = 1,
      ["method"] = method,
      ["params"] = parameters
    });

    using HttpResponseMessage response = await HttpClient.PostAsync(URL_RPC, new ByteArrayContent(request)).ConfigureAwait(false);

    using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false));

    JsonElement root = document.RootElement;

    if (root.TryGetProperty("error", out JsonElement error))
    {
      List<string> parametersFaulty = [];

      if (error.TryGetProperty("data", out JsonElement data) && data.TryGetProperty("parameters", out JsonElement parametersJSON))
        parametersFaulty = parametersJSON.EnumerateArray().Select(p => p.GetString()).ToList();

      throw new RPCException(error.GetProperty("message").GetString(), parametersFaulty);
    }

    return root.GetProperty("result").Clone();
  }
}
