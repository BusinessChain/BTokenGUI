using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public partial class Chip : Good
  {
    public int Index { get; }

    public string HashBlock { get; }

    [ObservableProperty]
    public partial TX TX { get; set; }

    string NameBlockchain;
    bool IsLoadingTX;


    public Chip()
    { }

    public Chip(JsonElement output, string nameBlockchain)
    {
      Hash = output.GetProperty("hashTX").GetString();
      Index = output.GetProperty("index").GetInt32();
      HashBlock = output.GetProperty("hashBlock").GetString();
      NameBlockchain = nameBlockchain;
    }

    public async Task LoadTX()
    {
      if (TX != null || IsLoadingTX)
        return;

      IsLoadingTX = true;

      List<JsonElement> tXsJSON = await LoadTXsBlock(NameBlockchain, HashBlock);

      if (tXsJSON?.FirstOrDefault(tX => tX.GetProperty("hash").GetString() == Hash) is JsonElement tXJSON && tXJSON.ValueKind == JsonValueKind.Object)
        TX = new TX(tXJSON);

      IsLoadingTX = false;
    }
  }
}
