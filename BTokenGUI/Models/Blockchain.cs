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
  public ObservableCollection<Anchor> Anchors { get; } = [];
  public ObservableCollection<Anchor> Flights { get; } = [];
  public ObservableCollection<Block> BlocksMined { get; } = [];
  public bool IsTipLast { get; }
  public bool IsTXPoolVisible { get; }
  public bool IsBlocksMinedPoolVisible { get; }
  bool IsAnchorsPlaced;


  public Blockchain(string name, bool isTipLast, bool isTXPoolVisible, bool isBlocksMinedPoolVisible)
  {
    Name = name;
    IsTipLast = isTipLast;
    IsTXPoolVisible = isTXPoolVisible;
    IsBlocksMinedPoolVisible = isBlocksMinedPoolVisible;
  }

  public void Update(JsonElement snapshotState)
  {
    Dictionary<string, (Anchor Anchor, ObservableCollection<Anchor> Place)> placesAnchor = Anchors
      .Select(anchor => (anchor, Anchors))
      .Concat(Blocks.SelectMany(block => block.Anchors.Select(anchor => (anchor, block.Anchors))))
      .ToDictionary(placeAnchor => placeAnchor.anchor.Hash);

    List<JsonElement> blocksJSON = snapshotState.GetProperty("headers").EnumerateArray().ToList();

    if (IsTipLast)
      blocksJSON.Reverse();

    SyncBlocks(Blocks, blocksJSON);

    if (snapshotState.TryGetProperty("blocksAwaitingAnchor", out JsonElement blocksAwaitingAnchor))
      SyncBlocks(BlocksMined, blocksAwaitingAnchor.EnumerateArray().ToList());

    if (snapshotState.TryGetProperty("anchorer", out JsonElement anchorer))
      PlaceAnchors(anchorer.GetProperty("anchors"), placesAnchor);
  }

  static void SyncBlocks(ObservableCollection<Block> blocks, List<JsonElement> blocksJSON)
  {
    HashSet<string> hashes = blocksJSON.Select(block => block.GetProperty("hash").GetString()).ToHashSet();

    foreach (Block block in blocks.Where(block => !hashes.Contains(block.Hash)).ToList())
      blocks.Remove(block);

    for (int index = 0; index < blocksJSON.Count; index++)
    {
      string hash = blocksJSON[index].GetProperty("hash").GetString();

      if (!blocks.Any(block => block.Hash == hash))
        blocks.Insert(index, new Block(blocksJSON[index]));
    }
  }

  void PlaceAnchors(JsonElement anchorsJSON, Dictionary<string, (Anchor Anchor, ObservableCollection<Anchor> Place)> placesAnchor)
  {
    foreach (JsonElement anchorJSON in anchorsJSON.EnumerateArray())
    {
      string hash = anchorJSON.GetProperty("hash").GetString();

      if (Flights.Any(anchorFlying => anchorFlying.Hash == hash))
        continue;

      Anchor anchor;
      ObservableCollection<Anchor> place;

      if (placesAnchor.Remove(hash, out (Anchor Anchor, ObservableCollection<Anchor> Place) placeAnchor))
        (anchor, place) = placeAnchor;
      else
        (anchor, place) = (new Anchor(anchorJSON), null);

      anchor.Update(anchorJSON);

      ObservableCollection<Anchor> destination = anchor.HeightConfirmed == 0
        ? Anchors
        : Blocks.FirstOrDefault(block => block.Height == anchor.HeightConfirmed)?.Anchors;

      if (destination == place)
        continue;

      place?.Remove(anchor);

      if (destination == null)
        continue;

      if (IsAnchorsPlaced)
      {
        anchor.Origin = place;
        anchor.Destination = destination;
        Flights.Add(anchor);
      }
      else
        destination.Add(anchor);
    }

    foreach ((Anchor anchor, ObservableCollection<Anchor> place) in placesAnchor.Values)
      place.Remove(anchor);

    IsAnchorsPlaced = true;
  }

  public void Land(Anchor anchor)
  {
    Flights.Remove(anchor);
    anchor.Destination.Add(anchor);

    anchor.Origin = null;
    anchor.Destination = null;
  }

  public class Block
  {
    public string Hash { get; }
    public int Height { get; }
    public string HashShort { get; }
    public List<Field> Fields { get; }
    public ObservableCollection<Anchor> Anchors { get; } = [];


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
