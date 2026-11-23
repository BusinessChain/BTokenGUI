using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public class TX
  {
    public List<Block.Field> Fields { get; }

    public List<List<Block.Field>> Inputs { get; }

    public List<List<Block.Field>> Outputs { get; }


    public TX(JsonElement tX)
    {
      Fields = ParseFields(tX);
      Inputs = tX.TryGetProperty("inputs", out JsonElement inputs) ? inputs.EnumerateArray().Select(ParseFields).ToList() : [];
      Outputs = tX.GetProperty("outputs").EnumerateArray().Select(ParseFields).ToList();
    }

    static List<Block.Field> ParseFields(JsonElement element)
    {
      return element.EnumerateObject()
        .Where(property => property.Value.ValueKind != JsonValueKind.Array && property.Name is not ("isMissed" or "heightConfirmed"))
        .Select(property => new Block.Field(property.Name, property.Value.ToString()))
        .ToList();
    }
  }
}
