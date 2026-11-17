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

public partial class MainViewModel : ViewModelBase
{
  [ObservableProperty]
  public partial string StatusConnection { get; set; } = "Connecting to node";

  [ObservableProperty]
  public partial string TipBlock { get; set; } = "No block received yet";

  public ObservableCollection<Network> Networks { get; } = [];

  const int MILLISECONDS_INTERVAL_POLL_NETWORKS = 1000;


  public MainViewModel(RPCClient rPCClient)
  {
    rPCClient.OnConnectionChanged += isConnected =>
      Dispatcher.UIThread.Post(() => StatusConnection = isConnected ? "Connected to node" : "Connecting to node");

    rPCClient.OnNotification += HandleNotification;

    _ = PollNetworks(rPCClient);
  }

  async Task PollNetworks(RPCClient rPCClient)
  {
    while (true)
    {
      await Task.Delay(MILLISECONDS_INTERVAL_POLL_NETWORKS).ConfigureAwait(false);

      try
      {
        JsonElement result = await rPCClient.SendRequest("getNetworks", null).ConfigureAwait(false);

        Dispatcher.UIThread.Post(() =>
        {
          foreach (JsonProperty networkJSON in result.EnumerateObject())
          {
            Network network = Networks.FirstOrDefault(n => n.Name == networkJSON.Name);

            if (network == null)
            {
              network = new Network(networkJSON.Name);
              Networks.Add(network);
            }

            network.Update(networkJSON.Value);
          }
        });
      }
      catch (Exception exception) when (exception is InvalidOperationException or WebSocketException or ObjectDisposedException)
      { }
    }
  }

  void HandleNotification(string method, JsonElement parameters)
  {
    if (method != "blockInserted" && method != "blockRolledBack")
      return;

    string prefix = method == "blockRolledBack" ? "Rolled back to " : "";
    string tip = $"{prefix}block {parameters.GetProperty("height").GetInt32()}, {parameters.GetProperty("hash").GetString()[..16]}";

    Dispatcher.UIThread.Post(() => TipBlock = tip);
  }
}
