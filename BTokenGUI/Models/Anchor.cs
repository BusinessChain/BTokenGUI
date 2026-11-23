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

    [ObservableProperty]
    public partial bool IsMissed { get; set; }

    [ObservableProperty]
    public partial bool IsRotten { get; set; }


    public Anchor(JsonElement anchor)
    {
      Hash = anchor.GetProperty("hash").GetString();
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
