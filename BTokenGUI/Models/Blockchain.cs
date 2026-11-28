using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTokenGUI.Models;

public partial class Blockchain : ObservableObject
{
  const int COUNT_POLLS_CHASE = 7;

  public string Name { get; }
  public ObservableCollection<Block> Blocks { get; } = [];
  public ObservableCollection<Anchor> Anchors { get; } = [];
  public ObservableCollection<Chip> TXsPool { get; } = [];
  public Mining.Wallet Tank { get; }
  [ObservableProperty]
  public partial bool IsPlaced { get; set; }
  [ObservableProperty]
  public partial double FillingPowder { get; set; }
  [ObservableProperty]
  public partial bool IsFormAnchorsOpen { get; set; }
  [ObservableProperty]
  public partial bool IsFormTXsPoolOpen { get; set; }
  public IBrush BrushTip { get; private set; }
  public DateTime TimeStartChase { get; private set; }
  public DateTime TimeStartChorus { get; private set; }
  Blockchain BlockchainParent;
  string HashTip;
  int CountPollsChase;
  long? FundsTank;
  ObservableCollection<Good> Flights;


  public Blockchain(string name, Blockchain blockchainParent, Mining.Wallet tank, ObservableCollection<Good> flights)
  {
    Name = name;
    BlockchainParent = blockchainParent;
    Tank = tank;
    Flights = flights;
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

  [RelayCommand]
  void OpenFormTXsPool()
  {
    IsFormTXsPoolOpen = true;
  }

  [RelayCommand]
  void CloseFormTXsPool()
  {
    IsFormTXsPoolOpen = false;
  }

  public void Update(JsonElement snapshotState)
  {
    Dictionary<string, (Anchor Anchor, ObservableCollection<Anchor> Place)> placesAnchor = Anchors
      .Select(anchor => (anchor, Anchors))
      .Concat(Blocks.SelectMany(block => block.Anchors.Select(anchor => (anchor, block.Anchors))))
      .ToDictionary(placeAnchor => placeAnchor.anchor.Hash);

    PlaceBlocks(snapshotState.GetProperty("headers").EnumerateArray().Reverse().ToList());

    bool hasAnchorer = snapshotState.TryGetProperty("anchorer", out JsonElement anchorer);

    PlaceChips(hasAnchorer ? anchorer : null);

    if (hasAnchorer)
      PlaceAnchors(anchorer.GetProperty("anchors"), placesAnchor);

    if (snapshotState.TryGetProperty("tXPool", out JsonElement tXPool))
      PlaceTXsPool(tXPool);

    UpdateLights(snapshotState.GetProperty("tip").GetProperty("hash").GetString());

    IsPlaced = true;
  }

  void UpdateLights(string hashTip)
  {
    if (hashTip != HashTip)
    {
      if (HashTip != null)
        TimeStartChorus = DateTime.UtcNow;

      HashTip = hashTip;

      if (BlockchainParent != null)
        BrushTip = Good.CreateBrushPair(hashTip);
    }

    CountPollsChase += 1;

    if (CountPollsChase == COUNT_POLLS_CHASE)
    {
      TimeStartChase = DateTime.UtcNow;
      CountPollsChase = 0;
    }
  }

  void PlaceTXsPool(JsonElement tXPool)
  {
    Dictionary<string, JsonElement> tXsWithoutChip = tXPool.EnumerateArray()
      .ToDictionary(tXJSON => tXJSON.GetProperty("hash").GetString());

    foreach (Chip chip in TXsPool.ToList())
      if (!tXsWithoutChip.Remove(chip.Hash))
        TXsPool.Remove(chip);

    foreach (JsonElement tXJSON in tXsWithoutChip.Values)
      TXsPool.Add(new Chip(tXJSON));
  }

  void PlaceBlocks(List<JsonElement> blocksJSON)
  {
    HashSet<string> hashesBlock = blocksJSON.Select(blockJSON => blockJSON.GetProperty("hash").GetString()).ToHashSet();

    foreach (Block block in Blocks.Where(block => !hashesBlock.Contains(block.Hash)).ToList())
      Blocks.Remove(block);

    int indexInsert = 0;

    foreach (JsonElement blockJSON in blocksJSON)
    {
      string hash = blockJSON.GetProperty("hash").GetString();
      int indexBlock = Blocks.ToList().FindIndex(block => block.Hash == hash);

      if (indexBlock < 0)
      {
        indexBlock = indexInsert;
        Blocks.Insert(indexBlock, new Block(blockJSON, Name, BlockchainParent != null));
      }

      indexInsert = indexBlock + 1;
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
    if (Tank.Funds is not long funds || Blocks.Count == 0 || Blocks[^1].IsFlying)
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
    HashSet<(string, int)> outputsConsumed = anchorer.GetProperty("anchors").EnumerateArray()
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
        chip.Chain = this;
        chip.IsFlying = true;
        Flights.Add(chip);
      }
      else
        chip.IsExploding = true;
    }

    foreach (JsonElement outputJSON in outputsWithoutChip.Values)
    {
      Chip chip = new(outputJSON, Name);

      Move(chip, Blocks.FirstOrDefault(block => block.Hash == chip.HashBlock) ?? Blocks[^1], Tank.Chips, Tank.Chips.Count);
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
    good.Chain = this;
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
