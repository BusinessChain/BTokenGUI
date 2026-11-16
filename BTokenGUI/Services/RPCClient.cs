using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BTokenGUI.Services;

public class RPCClient
{
  const string URL_RPC = "ws://127.0.0.1:8778/";
  const int MILLISECONDS_DELAY_RECONNECT = 2000;

  public event Action<string, JsonElement> OnNotification;
  public event Action<bool> OnConnectionChanged;

  ClientWebSocket WebSocket;
  SemaphoreSlim SemaphoreSend = new(1);
  long IdRequestLast;
  ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> RequestsPending = new();


  public async Task Run()
  {
    while (true)
    {
      using ClientWebSocket webSocket = new();

      try
      {
        await webSocket.ConnectAsync(new Uri(URL_RPC), CancellationToken.None).ConfigureAwait(false);

        WebSocket = webSocket;

        OnConnectionChanged?.Invoke(true);

        try
        {
          while (await ReceiveMessage(webSocket).ConfigureAwait(false) is byte[] message)
            Dispatch(message);
        }
        finally
        {
          WebSocket = null;

          foreach (long id in RequestsPending.Keys)
            if (RequestsPending.TryRemove(id, out TaskCompletionSource<JsonElement> request))
              request.TrySetException(new WebSocketException("Connection to node lost."));

          OnConnectionChanged?.Invoke(false);
        }
      }
      catch (WebSocketException)
      { }

      await Task.Delay(MILLISECONDS_DELAY_RECONNECT).ConfigureAwait(false);
    }
  }

  public async Task<JsonElement> SendRequest(string method, object parameters)
  {
    ClientWebSocket webSocket = WebSocket ?? throw new InvalidOperationException("Not connected to node.");

    long id = Interlocked.Increment(ref IdRequestLast);

    TaskCompletionSource<JsonElement> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
    RequestsPending[id] = response;

    byte[] request = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
    {
      ["jsonrpc"] = "2.0",
      ["id"] = id,
      ["method"] = method,
      ["params"] = parameters
    });

    await SemaphoreSend.WaitAsync().ConfigureAwait(false);

    try
    {
      await webSocket.SendAsync(
        new ArraySegment<byte>(request),
        WebSocketMessageType.Text,
        endOfMessage: true,
        CancellationToken.None).ConfigureAwait(false);
    }
    catch
    {
      RequestsPending.TryRemove(id, out _);
      throw;
    }
    finally
    {
      SemaphoreSend.Release();
    }

    return await response.Task.ConfigureAwait(false);
  }

  static async Task<byte[]> ReceiveMessage(WebSocket webSocket)
  {
    using MemoryStream message = new();
    byte[] buffer = new byte[4096];
    WebSocketReceiveResult result;

    do
    {
      result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ConfigureAwait(false);

      if (result.MessageType == WebSocketMessageType.Close)
        return null;

      message.Write(buffer, 0, result.Count);
    }
    while (!result.EndOfMessage);

    return message.ToArray();
  }

  void Dispatch(byte[] message)
  {
    try
    {
      using JsonDocument document = JsonDocument.Parse(message);

      JsonElement root = document.RootElement;

      if (root.TryGetProperty("id", out JsonElement id))
      {
        if (id.ValueKind == JsonValueKind.Number && RequestsPending.TryRemove(id.GetInt64(), out TaskCompletionSource<JsonElement> request))
          if (root.TryGetProperty("error", out JsonElement error))
            request.SetException(new InvalidOperationException(error.GetProperty("message").GetString()));
          else
            request.SetResult(root.GetProperty("result").Clone());
      }
      else if (root.TryGetProperty("method", out JsonElement method))
      {
        root.TryGetProperty("params", out JsonElement parameters);
        OnNotification?.Invoke(method.GetString(), parameters.Clone());
      }
    }
    catch (JsonException)
    { }
  }
}
