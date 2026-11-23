using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public partial class Anchor : Good
  {
    public TX TX { get; }

    public int HeightConfirmed { get; private set; }

    public string HashBlockReferenced { get; }

    public int LockTime { get; }

    public bool IsMissed { get; private set; }

    [ObservableProperty]
    public partial string ReasonForNotGlowing { get; set; }


    public Anchor(JsonElement anchor)
    {
      Hash = anchor.GetProperty("hash").GetString();
      LockTime = anchor.GetProperty("lockTime").GetInt32();
      TX = new TX(anchor);
      HashBlockReferenced = anchor.GetProperty("outputs").EnumerateArray()
        .First(output => output.TryGetProperty("hashBlockReferenced", out _))
        .GetProperty("hashBlockReferenced").GetString();
      BrushPair = CreateBrushPair(HashBlockReferenced);
    }

    public void Update(JsonElement anchor)
    {
      IsMissed = anchor.GetProperty("isMissed").GetBoolean();
      HeightConfirmed = anchor.GetProperty("heightConfirmed").GetInt32();
    }
  }
}
