using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public partial class Anchor : ObservableObject
  {
    public string Hash { get; }

    public List<Block.Field> Fields { get; }

    public List<List<Block.Field>> Inputs { get; }

    public List<List<Block.Field>> Outputs { get; }

    public int HeightConfirmed { get; private set; }

    public ObservableCollection<Anchor> Origin { get; set; }

    public ObservableCollection<Anchor> Destination { get; set; }

    [ObservableProperty]
    public partial bool IsMissed { get; set; }


    public Anchor(JsonElement anchor)
    {
      Hash = anchor.GetProperty("hash").GetString();
      Fields = ParseFields(anchor);
      Inputs = anchor.GetProperty("inputs").EnumerateArray().Select(ParseFields).ToList();
      Outputs = anchor.GetProperty("outputs").EnumerateArray().Select(ParseFields).ToList();
    }

    static List<Block.Field> ParseFields(JsonElement element)
    {
      return element.EnumerateObject()
        .Where(property => property.Value.ValueKind != JsonValueKind.Array && property.Name is not ("isMissed" or "heightConfirmed"))
        .Select(property => new Block.Field(property.Name, property.Value.ToString()))
        .ToList();
    }

    public void Update(JsonElement anchor)
    {
      IsMissed = anchor.GetProperty("isMissed").GetBoolean();
      HeightConfirmed = anchor.GetProperty("heightConfirmed").GetInt32();
    }
  }
}
