using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public class NetworkSnapshot
{
  public string Name { get; }
  public ObservableCollection<Peer> Peers { get; } = [];


  public NetworkSnapshot(string name)
  {
    Name = name;
  }

  public void Update(JsonElement peers)
  {
    HashSet<string> iPs = peers.EnumerateArray().Select(peer => peer.GetProperty("ip").GetString()).ToHashSet();

    foreach (Peer peer in Peers.Where(peer => !iPs.Contains(peer.IP)).ToList())
      Peers.Remove(peer);

    foreach (JsonElement peerJSON in peers.EnumerateArray())
    {
      string iP = peerJSON.GetProperty("ip").GetString();
      Peer peer = Peers.FirstOrDefault(p => p.IP == iP);

      if (peer == null)
      {
        peer = new Peer(iP);
        Peers.Add(peer);
      }

      peer.Update(peerJSON);
    }
  }
}

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
  public partial List<MessageSnapshot> Messages { get; set; }


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
    Messages = peer.GetProperty("messages").EnumerateObject().Select(message => new MessageSnapshot(
      message.Name,
      message.Value.GetProperty("level").GetDouble(),
      message.Value.GetProperty("levelMax").GetInt32())).ToList();
  }
}

public record MessageSnapshot(string Command, double Level, int LevelMax);
