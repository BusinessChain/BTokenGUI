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

  [ObservableProperty]
  public partial Miner MinerBlockchain { get; set; }

  [ObservableProperty]
  public partial string AddressReceiving { get; set; }

  [ObservableProperty]
  public partial string BalanceWallet { get; set; }


  public Blockchain(string name)
  {
    Name = name;
  }

  public void Update(JsonElement snapshotState)
  {
    List<JsonElement> blocksJSON = snapshotState.GetProperty("headers").EnumerateArray().ToList();
    HashSet<string> hashes = blocksJSON.Select(block => block.GetProperty("hash").GetString()).ToHashSet();

    foreach (Block block in Blocks.Where(block => !hashes.Contains(block.Hash)).ToList())
      Blocks.Remove(block);

    for (int index = 0; index < blocksJSON.Count; index++)
    {
      string hash = blocksJSON[index].GetProperty("hash").GetString();

      if (!Blocks.Any(block => block.Hash == hash))
        Blocks.Insert(index, new Block(blocksJSON[index]));
    }

    JsonElement snapshotWallet = snapshotState.GetProperty("wallet");
    JsonElement balance = snapshotWallet.GetProperty("balance");

    AddressReceiving = snapshotWallet.GetProperty("addressReceiving").GetString();
    BalanceWallet = balance.ValueKind == JsonValueKind.Null ? "unknown while the accounts synchronize" : balance.GetInt64().ToString();

    if (snapshotState.TryGetProperty("miner", out JsonElement snapshotMiner))
    {
      MinerBlockchain ??= new Miner();
      MinerBlockchain.Update(snapshotMiner, snapshotState.GetProperty("isAccountsAtTip").GetBoolean());
    }
  }

  public class Block
  {
    public string Hash { get; }
    public int Height { get; }
    public string HashShort { get; }
    public List<Field> Fields { get; }


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
