using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public partial class Block : Good
  {
    [ObservableProperty]
    public partial bool IsExploding { get; set; }

    public int Height { get; }
    public string HashShort { get; }
    public List<Field> Fields { get; }
    public ObservableCollection<Anchor> Anchors { get; } = [];
    public HashSet<string> HashesAnchorWinner { get; }

    [ObservableProperty]
    public partial List<TX> TXs { get; set; }

    string NameBlockchain;
    bool IsLoadingTXs;


    public Block(JsonElement block, string nameBlockchain)
    {
      NameBlockchain = nameBlockchain;
      Hash = block.GetProperty("hash").GetString();
      BrushPair = CreateBrushPair(Hash);
      Height = block.GetProperty("height").GetInt32();
      HashShort = $"{Hash[..8]} ... {Hash[^8..]}";
      HashesAnchorWinner = block.GetProperty("anchorsWinner").EnumerateArray()
        .Select(anchor => anchor.GetProperty("hashBlockReferenced").GetString())
        .ToHashSet();
      Fields = [];

      foreach (JsonProperty property in block.EnumerateObject())
      {
        if (property.Value.ValueKind == JsonValueKind.Array)
          continue;

        Fields.Add(new Field(property.Name, property.Value.ToString()));

        if (property.Name == "unixTimeSeconds")
          Fields.Add(new Field("timestamp", DateTimeOffset.FromUnixTimeSeconds(property.Value.GetInt64()).ToLocalTime().ToString("G")));
      }
    }

    public async Task LoadTXs()
    {
      if (TXs != null || IsLoadingTXs)
        return;

      IsLoadingTXs = true;

      try
      {
        JsonElement tXsJSON = await Node.SendRequest("getTXsBlock", new Dictionary<string, object>
        {
          ["blockchain"] = NameBlockchain,
          ["hash"] = Hash
        });

        if (tXsJSON.ValueKind == JsonValueKind.Array)
          TXs = tXsJSON.EnumerateArray().Select(tX => new TX(tX)).ToList();
      }
      catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or RPCException)
      { }
      finally
      {
        IsLoadingTXs = false;
      }
    }

    public record Field(string Name, string Value);
  }
}
