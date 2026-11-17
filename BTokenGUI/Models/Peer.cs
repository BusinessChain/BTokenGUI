using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Peer : ObservableObject
{
  public string IP { get; }

  [ObservableProperty]
  public partial string Connection { get; set; }

  [ObservableProperty]
  public partial string StateCurrent { get; set; }

  [ObservableProperty]
  public partial bool IsDisposed { get; set; }

  [ObservableProperty]
  public partial DateTime TimeConnected { get; set; }

  [ObservableProperty]
  public partial List<DOSMonitor> DOSMonitors { get; set; }


  public Peer(string iP)
  {
    IP = iP;
  }

  public void Update(JsonElement peer)
  {
    Connection = peer.GetProperty("connection").GetString();
    StateCurrent = peer.GetProperty("stateCurrent").GetString();
    IsDisposed = peer.GetProperty("isDisposed").GetBoolean();
    TimeConnected = peer.GetProperty("timeConnected").GetDateTime().ToLocalTime();
    DOSMonitors = peer.GetProperty("messages").EnumerateObject().Select(message => new DOSMonitor(
      message.Name,
      message.Value.GetProperty("level").GetDouble(),
      message.Value.GetProperty("levelMax").GetInt32())).ToList();
  }

  public record DOSMonitor(string Command, double Level, int LevelMax);
}
