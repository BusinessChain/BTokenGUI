using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTokenGUI.Models;

public partial class Mining
{
  public partial class Anchor : ObservableObject
  {
    Mining Mining;

    public string Hash { get; }

    public List<Blockchain.Block.Field> Fields { get; }

    public List<List<Blockchain.Block.Field>> Inputs { get; }

    public List<List<Blockchain.Block.Field>> Outputs { get; }

    [ObservableProperty]
    public partial bool IsMissed { get; set; }


    public Anchor(Mining mining, JsonElement anchor)
    {
      Mining = mining;
      Hash = anchor.GetProperty("hash").GetString();
      Fields = ParseFields(anchor);
      Inputs = anchor.GetProperty("inputs").EnumerateArray().Select(ParseFields).ToList();
      Outputs = anchor.GetProperty("outputs").EnumerateArray().Select(ParseFields).ToList();
    }

    static List<Blockchain.Block.Field> ParseFields(JsonElement element)
    {
      return element.EnumerateObject()
        .Where(property => property.Value.ValueKind != JsonValueKind.Array && property.Name != "isMissed")
        .Select(property => new Blockchain.Block.Field(property.Name, property.Value.ToString()))
        .ToList();
    }

    public void Update(JsonElement anchor)
    {
      IsMissed = anchor.GetProperty("isMissed").GetBoolean();
    }

    [RelayCommand]
    void OpenPanel()
    {
      Mining.AnchorShown = this;
    }
  }
}
