using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain : ObservableObject
{
  const int COUNT_CHIPS_TANK_FULL = 12;
  const int COUNT_CHIPS_ARRIVAL = 3;

  public string Name { get; }
  public ObservableCollection<Block> Blocks { get; } = [];
  public ObservableCollection<Anchor> Anchors { get; } = [];
  public ObservableCollection<Good> Flights { get; } = [];
  public ObservableCollection<Block> BlocksMined { get; } = [];
  public ObservableCollection<Chip> Chips { get; } = [];
  public Mining.Wallet Tank { get; }
  public bool IsAboveMiner { get; }
  public bool IsTXPoolVisible { get; }
  public bool IsBlocksMinedPoolVisible { get; }
  public bool IsTipExtending;
  public Task TaskSlideBlocks = Task.CompletedTask;
  [ObservableProperty]
  public partial bool IsPlaced { get; set; }
  Blockchain BlockchainParent;
  long? FundsTank;


  public Blockchain(string name, bool isAboveMiner, bool isTXPoolVisible, bool isBlocksMinedPoolVisible, Blockchain blockchainParent, Mining.Wallet tank)
  {
    Name = name;
    IsAboveMiner = isAboveMiner;
    IsTXPoolVisible = isTXPoolVisible;
    IsBlocksMinedPoolVisible = isBlocksMinedPoolVisible;
    BlockchainParent = blockchainParent;
    Tank = tank;
  }

  public void Update(JsonElement snapshotState)
  {
    if (!TaskSlideBlocks.IsCompleted)
      return;

    Dictionary<string, (Anchor Anchor, ObservableCollection<Anchor> Place)> placesAnchor = Anchors
      .Select(anchor => (anchor, Anchors))
      .Concat(Blocks.SelectMany(block => block.Anchors.Select(anchor => (anchor, block.Anchors))))
      .ToDictionary(placeAnchor => placeAnchor.anchor.Hash);

    List<JsonElement> blocksJSON = snapshotState.GetProperty("headers").EnumerateArray().Reverse().ToList();

    List<JsonElement> blocksMinedJSON = snapshotState.TryGetProperty("blocksAwaitingAnchor", out JsonElement blocksAwaitingAnchor)
      ? blocksAwaitingAnchor.EnumerateArray().ToList()
      : [];

    IsTipExtending = IsPlaced
      && blocksJSON.Count > 1
      && blocksJSON.Count == Blocks.Count
      && blocksJSON[^2].GetProperty("hash").GetString() == Blocks[^1].Hash;

    PlaceBlocks(blocksJSON, blocksMinedJSON);

    IsTipExtending = false;

    if (TaskSlideBlocks.IsCompleted && snapshotState.TryGetProperty("anchorer", out JsonElement anchorer))
      PlaceAnchors(anchorer.GetProperty("anchors"), placesAnchor);

    PlaceChips();

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
            Move(new Block(blockJSON, Name, BlockchainParent != null), null, destination, indexInsert);
          else
            destination.Insert(indexInsert, new Block(blockJSON, Name, BlockchainParent != null));
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

      int heightEligible = anchor.LockTime + 1;

      if (anchor.IsMissed)
        anchor.ReasonForNotGlowing = $"The anchor was not mined in Bitcoin block {heightEligible}, the only block in which it can win.";
      else if (blockConfirmed != null && anchor.HeightConfirmed != heightEligible)
        anchor.ReasonForNotGlowing = $"The anchor was mined in Bitcoin block {anchor.HeightConfirmed}, but it can win only in Bitcoin block {heightEligible}.";
      else if (blockConfirmed != null && !blockConfirmed.HashesAnchorWinner.Contains(anchor.HashBlockReferenced))
        anchor.ReasonForNotGlowing = $"Another anchor won the draw in Bitcoin block {anchor.HeightConfirmed}.";
      else
        anchor.ReasonForNotGlowing = null;

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

  void PlaceChips()
  {
    if (Tank?.Funds is not long funds || !TaskSlideBlocks.IsCompleted || Blocks.Count == 0 || Blocks[^1].IsFlying)
      return;

    foreach (Chip chip in Chips.Where(chip => chip.IsExploding && !chip.IsFlying).ToList())
      Chips.Remove(chip);

    int countChipsFilling = (int)Math.Ceiling(Tank.Filling * COUNT_CHIPS_TANK_FULL);

    foreach (Chip chip in Chips.Skip(countChipsFilling).Where(chip => !chip.IsFlying))
      chip.IsExploding = true;

    int countChipsNew = Math.Max(countChipsFilling - Chips.Count, funds > FundsTank ? COUNT_CHIPS_ARRIVAL : 0);

    for (int i = 0; i < countChipsNew; i++)
      Move(new Chip(), Blocks[^1], Chips, Chips.Count);

    FundsTank = funds;
  }

  void Move<T>(T good, object origin, ObservableCollection<T> destination, int index) where T : Good
  {
    (origin as ObservableCollection<T>)?.Remove(good);
    good.Origin = origin;
    good.IsFlying = IsPlaced;
    destination.Insert(index, good);

    if (IsPlaced)
      Flights.Add(good);
  }

  public void Land(Good good)
  {
    Flights.Remove(good);

    good.IsFlying = false;
    good.Origin = null;
  }
}
