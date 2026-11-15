using System.Text.Json;
using Avalonia.Threading;
using BTokenGUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
  [ObservableProperty]
  public partial string StatusConnection { get; set; } = "Connecting to node";

  [ObservableProperty]
  public partial string TipBlock { get; set; } = "No block received yet";


  public MainViewModel(RPCClient rPCClient)
  {
    rPCClient.OnConnectionChanged += isConnected =>
      Dispatcher.UIThread.Post(() => StatusConnection = isConnected ? "Connected to node" : "Connecting to node");

    rPCClient.OnNotification += HandleNotification;
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
