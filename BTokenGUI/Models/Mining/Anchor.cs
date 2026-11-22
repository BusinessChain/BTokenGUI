using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Mining
{
  public partial class Anchor : ObservableObject
  {
    public string Hash { get; }

    [ObservableProperty]
    public partial bool IsMissed { get; set; }


    public Anchor(string hash)
    {
      Hash = hash;
    }

    public void Update(JsonElement anchor)
    {
      IsMissed = anchor.GetProperty("isMissed").GetBoolean();
    }
  }
}
