using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTokenGUI.Models;

public partial class Blockchain : ObservableObject
{
  public string Name { get; }
  public ObservableCollection<Block> Blocks { get; } = [];
  public ObservableCollection<Anchor> Anchors { get; } = [];
  public ObservableCollection<Good> Flights { get; } = [];
  public ObservableCollection<Block> BlocksMined { get; } = [];
  public Mining.Wallet Tank { get; }
  public bool IsTankAnchorVisible { get; }
  public bool IsBlocksMinedPoolVisible { get; }
  public bool IsTipExtending;
  public Task TaskSlideBlocks = Task.CompletedTask;
  [ObservableProperty]
  public partial bool IsPlaced { get; set; }
  [ObservableProperty]
  public partial double FillingPowder { get; set; }
  [ObservableProperty]
  public partial bool IsFormAnchorsOpen { get; set; }
  Blockchain BlockchainParent;
  long? FundsTank;


  public Blockchain(string name, bool isTankAnchorVisible, bool isBlocksMinedPoolVisible, Blockchain blockchainParent, Mining.Wallet tank)
  {
    Name = name;
    IsTankAnchorVisible = isTankAnchorVisible;
    IsBlocksMinedPoolVisible = isBlocksMinedPoolVisible;
    BlockchainParent = blockchainParent;
    Tank = tank;
  }

  [RelayCommand]
  void OpenFormAnchors()
  {
    IsFormAnchorsOpen = true;
  }

  [RelayCommand]
  void CloseFormAnchors()
  {
    IsFormAnchorsOpen = false;
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

    bool hasAnchorer = snapshotState.TryGetProperty("anchorer", out JsonElement anchorer);

    if (TaskSlideBlocks.IsCompleted && hasAnchorer)
      PlaceAnchors(anchorer.GetProperty("anchors"), placesAnchor);

    PlaceChips(hasAnchorer ? anchorer : null);

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

  void PlaceChips(JsonElement? anchorer)
  {
    if (Tank.Funds is not long funds || !TaskSlideBlocks.IsCompleted || Blocks.Count == 0 || Blocks[^1].IsFlying)
      return;

    foreach (Chip chip in Tank.Chips.Where(chip => chip.IsExploding && !chip.IsFlying).ToList())
      Tank.Chips.Remove(chip);

    if (anchorer is JsonElement anchorerJSON)
      PlaceChipsOutputs(anchorerJSON);
    else
      PlaceChipsPowder(funds);

    FundsTank = funds;
  }

  void PlaceChipsOutputs(JsonElement anchorer)
  {
    List<JsonElement> anchorsJSON = anchorer.GetProperty("anchors").EnumerateArray().ToList();

    HashSet<string> hashesAnchor = anchorsJSON.Select(anchorJSON => anchorJSON.GetProperty("hash").GetString()).ToHashSet();

    HashSet<(string, int)> outputsConsumed = anchorsJSON
      .SelectMany(anchorJSON => anchorJSON.GetProperty("inputs").EnumerateArray())
      .Select(inputJSON => (inputJSON.GetProperty("tXIDOutput").GetString(), inputJSON.GetProperty("indexOutput").GetInt32()))
      .ToHashSet();

    Dictionary<(string, int), JsonElement> outputsWithoutChip = anchorer.GetProperty("outputsSpendable").EnumerateArray()
      .ToDictionary(outputJSON => (outputJSON.GetProperty("hashTX").GetString(), outputJSON.GetProperty("index").GetInt32()));

    foreach (Chip chip in Tank.Chips.ToList())
    {
      if (outputsWithoutChip.Remove((chip.Hash, chip.Index)) || chip.IsFlying || chip.IsExploding)
        continue;

      if (outputsConsumed.Contains((chip.Hash, chip.Index)))
      {
        Tank.Chips.Remove(chip);
        chip.Origin = Tank.Chips;
        chip.IsFlying = true;
        Flights.Add(chip);
      }
      else
        chip.IsExploding = true;
    }

    foreach (JsonElement outputJSON in outputsWithoutChip.Values)
    {
      Chip chip = new(outputJSON, Name);

      object origin = hashesAnchor.Contains(chip.Hash) ? null : Blocks.FirstOrDefault(block => block.Hash == chip.HashBlock) ?? Blocks[^1];

      Move(chip, origin, Tank.Chips, Tank.Chips.Count);
    }
  }

  void PlaceChipsPowder(long funds)
  {
    if (IsPlaced && funds > FundsTank)
      Move(new Chip(), Blocks[^1], Tank.Chips, Tank.Chips.Count);

    if (!Tank.Chips.Any(chip => chip.IsFlying))
      FillingPowder = Tank.Filling;
  }

  static async Task<List<JsonElement>> LoadTXsBlock(string nameBlockchain, string hashBlock)
  {
    try
    {
      JsonElement tXsJSON = await Node.SendRequest("getTXsBlock", new Dictionary<string, object>
      {
        ["blockchain"] = nameBlockchain,
        ["hash"] = hashBlock
      });

      if (tXsJSON.ValueKind == JsonValueKind.Array)
        return tXsJSON.EnumerateArray().ToList();
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or RPCException)
    { }

    return null;
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

    if (good is Chip chip && !Tank.IsFeedingConverter)
    {
      chip.IsExploding = true;
      FillingPowder = Tank.Filling;
    }
  }
}
