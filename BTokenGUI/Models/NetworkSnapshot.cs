using System;
using System.Collections.Generic;

namespace BTokenGUI.Models;

public record NetworkSnapshot(string Name, List<PeerSnapshot> Peers);

public record PeerSnapshot(
  string IP,
  string Connection,
  string StateCurrent,
  bool IsDisposed,
  DateTime TimeConnected,
  List<MessageSnapshot> Messages)
{
  public bool IsExpanded { get; set; }
}

public record MessageSnapshot(string Command, double Level, int LevelMax);
