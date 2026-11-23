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
  public ObservableCollection<Good> Flights { get; } = [];
  public ObservableCollection<Block> BlocksMined { get; } = [];
  public bool IsTipLast { get; }
  public bool IsTXPoolVisible { get; }
  public bool IsBlocksMinedPoolVisible { get; }
  bool IsPlaced;
  Blockchain BlockchainParent;


  public Blockchain(string name, bool isTipLast, bool isTXPoolVisible, bool isBlocksMinedPoolVisible, Blockchain blockchainParent)
  {
    Name = name;
    IsTipLast = isTipLast;
    IsTXPoolVisible = isTXPoolVisible;
    IsBlocksMinedPoolVisible = isBlocksMinedPoolVisible;
    BlockchainParent = blockchainParent;
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

    List<JsonElement> blocksMinedJSON = snapshotState.TryGetProperty("blocksAwaitingAnchor", out JsonElement blocksAwaitingAnchor)
      ? blocksAwaitingAnchor.EnumerateArray().ToList()
      : [];

    PlaceBlocks(blocksJSON, blocksMinedJSON);

    if (BlockchainParent != null)
    {
      Dictionary<string, int> heightsParent = [];

      foreach (Block blockParent in BlockchainParent.Blocks)
        foreach (string hashBlockReferenced in blockParent.HashesAnchorWinner)
          heightsParent[hashBlockReferenced] = blockParent.Height;

      foreach (Block block in Blocks)
        block.HeightParent = heightsParent.TryGetValue(block.Hash, out int heightParent) ? heightParent : null;
    }

    if (snapshotState.TryGetProperty("anchorer", out JsonElement anchorer))
      PlaceAnchors(anchorer.GetProperty("anchors"), placesAnchor);

    IsPlaced = true;
  }

  void PlaceBlocks(List<JsonElement> headersJSON, List<JsonElement> blocksMinedJSON)
  {
    Dictionary<string, (Block Block, ObservableCollection<Block> Place)> placesBlock = Blocks
      .Select(block => (block, Blocks))
      .Concat(BlocksMined.Select(block => (block, BlocksMined)))
      .ToDictionary(placeBlock => placeBlock.block.Hash);

    (List<JsonElement> BlocksJSON, ObservableCollection<Block> Destination)[] destinationsBlocks = [(headersJSON, Blocks), (blocksMinedJSON, BlocksMined)];

    Dictionary<string, ObservableCollection<Block>> destinationsBlock = [];

    foreach ((List<JsonElement> blocksJSON, ObservableCollection<Block> destination) in destinationsBlocks)
      foreach (JsonElement blockJSON in blocksJSON)
        destinationsBlock.TryAdd(blockJSON.GetProperty("hash").GetString(), destination);

    foreach ((Block block, ObservableCollection<Block> place) in placesBlock.Values)
    {
      bool isVanished = !destinationsBlock.TryGetValue(block.Hash, out ObservableCollection<Block> destination);

      if (isVanished && place == BlocksMined && IsPlaced && !block.IsFlying && !block.IsExploding)
        block.IsExploding = true;
      else if (isVanished || destination != place && !block.IsFlying)
        place.Remove(block);
    }

    foreach ((List<JsonElement> blocksJSON, ObservableCollection<Block> destination) in destinationsBlocks)
    {
      int indexInsert = 0;

      foreach (JsonElement blockJSON in blocksJSON)
      {
        string hash = blockJSON.GetProperty("hash").GetString();

        if (destinationsBlock[hash] == destination)
        {
          if (placesBlock.TryGetValue(hash, out (Block Block, ObservableCollection<Block> Place) placeBlock))
          {
            if (!placeBlock.Block.IsFlying && placeBlock.Place != destination)
              Move(placeBlock.Block, placeBlock.Place, destination, indexInsert);
          }
          else if (destination == BlocksMined)
            Move(new Block(blockJSON, Name), null, destination, indexInsert);
          else
            destination.Insert(indexInsert, new Block(blockJSON, Name));
        }

        int indexBlock = destination.ToList().FindIndex(block => block.Hash == hash);

        if (indexBlock >= 0)
          indexInsert = indexBlock + 1;
      }
    }
  }

  void PlaceAnchors(JsonElement anchorsJSON, Dictionary<string, (Anchor Anchor, ObservableCollection<Anchor> Place)> placesAnchor)
  {
    foreach (JsonElement anchorJSON in anchorsJSON.EnumerateArray())
    {
      string hash = anchorJSON.GetProperty("hash").GetString();

      Anchor anchor;
      ObservableCollection<Anchor> place;

      if (placesAnchor.Remove(hash, out (Anchor Anchor, ObservableCollection<Anchor> Place) placeAnchor))
        (anchor, place) = placeAnchor;
      else
        (anchor, place) = (new Anchor(anchorJSON), null);

      if (anchor.IsFlying)
        continue;

      anchor.Update(anchorJSON);

      Block blockConfirmed = anchor.HeightConfirmed == 0 ? null : Blocks.FirstOrDefault(block => block.Height == anchor.HeightConfirmed);

      anchor.IsRotten = anchor.IsMissed || blockConfirmed != null && !blockConfirmed.HashesAnchorWinner.Contains(anchor.HashBlockReferenced);

      ObservableCollection<Anchor> destination = anchor.HeightConfirmed == 0 ? Anchors : blockConfirmed?.Anchors;

      if (destination == place)
        continue;

      if (destination == null)
      {
        place?.Remove(anchor);
        continue;
      }

      Move(anchor, place, destination, destination.Count);
    }

    foreach ((Anchor anchor, ObservableCollection<Anchor> place) in placesAnchor.Values)
      place.Remove(anchor);
  }

  void Move<T>(T good, ObservableCollection<T> place, ObservableCollection<T> destination, int index) where T : Good
  {
    place?.Remove(good);
    destination.Insert(index, good);

    if (!IsPlaced)
      return;

    good.Origin = place;
    good.IsFlying = true;
    Flights.Add(good);
  }

  public void Land(Good good)
  {
    Flights.Remove(good);

    good.IsFlying = false;
    good.Origin = null;
  }
}
