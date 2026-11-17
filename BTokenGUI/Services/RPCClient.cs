using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BTokenGUI.Services;

public class RPCClient
{
  const string URL_RPC = "http://127.0.0.1:8778/";

  HttpClient HttpClient = new();


  public async Task<JsonElement> SendRequest(string method, object parameters)
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
      throw new InvalidOperationException(error.GetProperty("message").GetString());

    return root.GetProperty("result").Clone();
  }
}
