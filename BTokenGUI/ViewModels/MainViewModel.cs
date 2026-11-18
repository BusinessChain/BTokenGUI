using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using BTokenGUI.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
  [ObservableProperty]
  public partial string StatusConnection { get; set; } = "Connecting to node";

  [ObservableProperty]
  public partial string BlocksNewest { get; set; } = "No blockchain state received yet";

  public ObservableCollection<Network> Networks { get; } = [];

  const string URL_RPC = "http://127.0.0.1:8778/";
  const int MILLISECONDS_INTERVAL_POLL_NODE = 1000;
  const int COUNT_BLOCKS_NEWEST = 6;

  HttpClient HttpClient = new();


  public MainViewModel()
  {
    _ = PollNode();
  }

  async Task PollNode()
  {
    while (true)
    {
      await Task.Delay(MILLISECONDS_INTERVAL_POLL_NODE).ConfigureAwait(false);

      try
      {
        JsonElement networksJSON = await SendRequest("getNetworks", null).ConfigureAwait(false);
        JsonElement blockchainsJSON = await SendRequest("getBlockchains", new Dictionary<string, object> { ["countBlocks"] = COUNT_BLOCKS_NEWEST }).ConfigureAwait(false);

        string blocksNewest = string.Join("\n\n", blockchainsJSON.EnumerateObject().Select(blockchainJSON =>
          blockchainJSON.Name + "\n" + string.Join("\n", blockchainJSON.Value.EnumerateArray().Select(blockJSON =>
          {
            string hash = blockJSON.GetProperty("hash").GetString();
            return $"{blockJSON.GetProperty("height").GetInt32()}  {hash[..8]} ... {hash[^8..]}";
          }))));

        Dispatcher.UIThread.Post(() =>
        {
          foreach (JsonProperty networkJSON in networksJSON.EnumerateObject())
          {
            Network network = Networks.FirstOrDefault(n => n.Name == networkJSON.Name);

            if (network == null)
            {
              network = new Network(networkJSON.Name);
              Networks.Add(network);
            }

            network.Update(networkJSON.Value);
          }

          BlocksNewest = blocksNewest;
          StatusConnection = "Connected to node";
        });
      }
      catch (Exception exception)
      {
        if (exception is not (HttpRequestException or TaskCanceledException or InvalidOperationException))
          Trace.WriteLine($"PollNode failed with {exception.GetType().Name}: {exception.Message}");

        Dispatcher.UIThread.Post(() => StatusConnection = "Connecting to node");
      }
    }
  }

  async Task<JsonElement> SendRequest(string method, object parameters)
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
