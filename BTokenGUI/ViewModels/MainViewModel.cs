using System;
using System.Collections.Generic;
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

  [ObservableProperty]
  public partial List<NetworkSnapshot> Networks { get; set; } = [];

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

        List<NetworkSnapshot> networks = result.EnumerateObject().Select(network => new NetworkSnapshot(
          network.Name,
          network.Value.EnumerateArray().Select(peer => new PeerSnapshot(
            peer.GetProperty("ip").GetString(),
            peer.GetProperty("connection").GetString(),
            peer.GetProperty("stateCurrent").GetString(),
            peer.GetProperty("isDisposed").GetBoolean(),
            peer.GetProperty("timeConnected").GetDateTime().ToLocalTime(),
            peer.GetProperty("messages").EnumerateObject().Select(message => new MessageSnapshot(
              message.Name,
              message.Value.GetProperty("level").GetDouble(),
              message.Value.GetProperty("levelMax").GetInt32())).ToList())).ToList())).ToList();

        Dispatcher.UIThread.Post(() => Networks = networks);
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
