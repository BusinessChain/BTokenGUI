using System.Text.Json;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public class Chip : Good
  {
    public int Index { get; }

    public string HashBlock { get; }


    public Chip()
    { }

    public Chip(JsonElement output)
    {
      Hash = output.GetProperty("hashTX").GetString();
      Index = output.GetProperty("index").GetInt32();
      HashBlock = output.GetProperty("hashBlock").GetString();
    }
  }
}
