using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using BTokenGUI.Models;
using BTokenGUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
  [ObservableProperty]
  public partial string StatusConnection { get; set; } = "Connecting to node";

  [ObservableProperty]
  public partial string TipsBlockchain { get; set; } = "No blockchain state received yet";

  public ObservableCollection<Network> Networks { get; } = [];

  const int MILLISECONDS_INTERVAL_POLL_NODE = 1000;


  public MainViewModel(RPCClient rPCClient)
  {
    rPCClient.OnConnectionChanged += isConnected =>
      Dispatcher.UIThread.Post(() => StatusConnection = isConnected ? "Connected to node" : "Connecting to node");

    _ = PollNode(rPCClient);
  }

  async Task PollNode(RPCClient rPCClient)
  {
    while (true)
    {
      await Task.Delay(MILLISECONDS_INTERVAL_POLL_NODE).ConfigureAwait(false);

      try
      {
        JsonElement networksJSON = await rPCClient.SendRequest("getNetworks", null).ConfigureAwait(false);
        JsonElement blockchainsJSON = await rPCClient.SendRequest("getBlockchains", null).ConfigureAwait(false);

        string tipsBlockchain = string.Join("\n", blockchainsJSON.EnumerateObject().Select(blockchainJSON =>
          $"{blockchainJSON.Name}: block {blockchainJSON.Value.GetProperty("height").GetInt32()}, {blockchainJSON.Value.GetProperty("hash").GetString()[..16]}"));

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

          TipsBlockchain = tipsBlockchain;
        });
      }
      catch (Exception exception) when (exception is InvalidOperationException or WebSocketException or ObjectDisposedException)
      { }
    }
  }
}
