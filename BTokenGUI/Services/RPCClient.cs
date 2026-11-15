using System;
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


  public async Task Run()
  {
    while (true)
    {
      using ClientWebSocket webSocket = new();

      try
      {
        await webSocket.ConnectAsync(new Uri(URL_RPC), CancellationToken.None).ConfigureAwait(false);

        OnConnectionChanged?.Invoke(true);

        try
        {
          while (await ReceiveMessage(webSocket).ConfigureAwait(false) is byte[] message)
            Dispatch(message);
        }
        finally
        {
          OnConnectionChanged?.Invoke(false);
        }
      }
      catch (WebSocketException)
      { }

      await Task.Delay(MILLISECONDS_DELAY_RECONNECT).ConfigureAwait(false);
    }
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

      if (!root.TryGetProperty("id", out _) && root.TryGetProperty("method", out JsonElement method))
      {
        root.TryGetProperty("params", out JsonElement parameters);
        OnNotification?.Invoke(method.GetString(), parameters.Clone());
      }
    }
    catch (JsonException)
    { }
  }
}
