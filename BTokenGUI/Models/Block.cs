using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public partial class Block : Good
  {
    public int Height { get; private set; }
    public long UnixTimeSeconds { get; private set; }
    public ObservableCollection<Anchor> Anchors { get; } = [];
    public HashSet<string> HashesAnchorWinner { get; private set; }

    [ObservableProperty]
    public partial List<Field> Fields { get; set; }

    [ObservableProperty]
    public partial List<TX> TXs { get; set; }

    string NameBlockchain;
    bool IsLoadingTXs;


    public Block(JsonElement block, string nameBlockchain, bool isPaired)
    {
      NameBlockchain = nameBlockchain;
      Hash = block.GetProperty("hash").GetString();

      if (isPaired)
        BrushPair = CreateBrushPair(Hash);

      Update(block);
    }

    public void Update(JsonElement block)
    {
      Height = block.GetProperty("height").GetInt32();
      HashesAnchorWinner = block.GetProperty("anchorsWinner").EnumerateArray()
        .Select(anchor => anchor.GetProperty("hashBlockReferenced").GetString())
        .ToHashSet();

      List<Field> fields = [];

      foreach (JsonProperty property in block.EnumerateObject())
      {
        if (property.Value.ValueKind == JsonValueKind.Array)
          continue;

        fields.Add(new Field(property.Name, property.Value.ToString()));

        if (property.Name == "unixTimeSeconds")
        {
          UnixTimeSeconds = property.Value.GetInt64();
          fields.Add(new Field("timestamp", DateTimeOffset.FromUnixTimeSeconds(UnixTimeSeconds).ToLocalTime().ToString("G")));
        }
      }

      Fields = fields;
    }

    public async Task LoadTXs()
    {
      if (TXs != null || IsLoadingTXs)
        return;

      IsLoadingTXs = true;

      List<JsonElement> tXsJSON = await LoadTXsBlock(NameBlockchain, Hash);

      if (tXsJSON != null)
        TXs = tXsJSON.Select(tX => new TX(tX)).ToList();

      IsLoadingTXs = false;
    }

    public record Field(string Name, string Value);
  }
}
