using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain : ObservableObject
{
  public string Name { get; }
  public ObservableCollection<Block> Blocks { get; } = [];
  bool IsTipLast;


  public Blockchain(string name, bool isTipLast)
  {
    Name = name;
    IsTipLast = isTipLast;
  }

  public void Update(JsonElement snapshotState)
  {
    List<JsonElement> blocksJSON = snapshotState.GetProperty("headers").EnumerateArray().ToList();

    if (IsTipLast)
      blocksJSON.Reverse();
    HashSet<string> hashes = blocksJSON.Select(block => block.GetProperty("hash").GetString()).ToHashSet();

    foreach (Block block in Blocks.Where(block => !hashes.Contains(block.Hash)).ToList())
      Blocks.Remove(block);

    for (int index = 0; index < blocksJSON.Count; index++)
    {
      string hash = blocksJSON[index].GetProperty("hash").GetString();

      if (!Blocks.Any(block => block.Hash == hash))
        Blocks.Insert(index, new Block(blocksJSON[index]));
    }
  }

  public void UpdateAnchors(IEnumerable<Mining.Anchor> anchors)
  {
    foreach (Block block in Blocks)
    {
      List<Mining.Anchor> anchorsBlock = anchors.Where(anchor => anchor.HeightConfirmed == block.Height).ToList();

      foreach (Mining.Anchor anchor in block.Anchors.Except(anchorsBlock).ToList())
        block.Anchors.Remove(anchor);

      foreach (Mining.Anchor anchor in anchorsBlock.Except(block.Anchors).ToList())
        block.Anchors.Add(anchor);
    }
  }

  public class Block
  {
    public string Hash { get; }
    public int Height { get; }
    public string HashShort { get; }
    public List<Field> Fields { get; }
    public ObservableCollection<Mining.Anchor> Anchors { get; } = [];


    public Block(JsonElement block)
    {
      Hash = block.GetProperty("hash").GetString();
      Height = block.GetProperty("height").GetInt32();
      HashShort = $"{Hash[..8]} ... {Hash[^8..]}";
      Fields = [];

      foreach (JsonProperty property in block.EnumerateObject())
      {
        Fields.Add(new Field(property.Name, property.Value.ToString()));

        if (property.Name == "unixTimeSeconds")
          Fields.Add(new Field("timestamp", DateTimeOffset.FromUnixTimeSeconds(property.Value.GetInt64()).ToLocalTime().ToString("G")));
      }
    }

    public record Field(string Name, string Value);
  }
}
