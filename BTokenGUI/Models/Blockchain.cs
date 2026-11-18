using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace BTokenGUI.Models;

public class Blockchain
{
  public string Name { get; }
  public ObservableCollection<Block> Blocks { get; } = [];


  public Blockchain(string name)
  {
    Name = name;
  }

  public void Update(JsonElement blocks)
  {
    List<JsonElement> blocksJSON = blocks.EnumerateArray().ToList();
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
      Fields = block.EnumerateObject().Select(property => new Field(property.Name, property.Value.ToString())).ToList();
    }

    public record Field(string Name, string Value);
  }
}
