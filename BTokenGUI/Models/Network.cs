using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace BTokenGUI.Models;

public class Network
{
  public string Name { get; }
  public ObservableCollection<Peer> Peers { get; } = [];


  public Network(string name)
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
