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
    List<Field> FieldsHeader;
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

      FieldsHeader = [];

      foreach (JsonProperty property in block.EnumerateObject())
      {
        if (property.Value.ValueKind == JsonValueKind.Array)
          continue;

        FieldsHeader.Add(new Field(property.Name, property.Value.ToString()));

        if (property.Name == "unixTimeSeconds")
          UnixTimeSeconds = property.Value.GetInt64();
      }
    }

    public async Task Open()
    {
      List<Field> fields = [];

      foreach (Field field in FieldsHeader)
      {
        fields.Add(field);

        if (field.Name == "unixTimeSeconds")
        {
          DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(UnixTimeSeconds);
          fields.Add(new Field("timestamp", $"{timestamp.ToLocalTime():G} ({FormatAge(DateTimeOffset.UtcNow - timestamp)})"));
        }
      }

      Fields = fields;

      if (TXs != null || IsLoadingTXs)
        return;

      IsLoadingTXs = true;

      List<JsonElement> tXsJSON = await LoadTXsBlock(NameBlockchain, Hash);

      if (tXsJSON != null)
        TXs = tXsJSON.Select(tX => new TX(tX)).ToList();

      IsLoadingTXs = false;
    }

    static string FormatAge(TimeSpan age)
    {
      if (age.TotalMinutes < 1)
        return $"{Math.Max(0, (int)age.TotalSeconds)} s";

      if (age.TotalHours < 1)
        return $"{(int)age.TotalMinutes} min";

      if (age.TotalDays < 1)
        return $"{(int)age.TotalHours} h {age.Minutes} min";

      return $"{(int)age.TotalDays} d {age.Hours} h";
    }

    public record Field(string Name, string Value);
  }
}
