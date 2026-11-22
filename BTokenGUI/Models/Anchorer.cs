using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public partial class Anchorer : OwnerWallet
  {
    [ObservableProperty]
    public partial long Funds { get; set; }

    [ObservableProperty]
    public partial string AddressFunding { get; set; }


    public void Update(JsonElement snapshotAnchorer)
    {
      Funds = snapshotAnchorer.GetProperty("funds").GetInt64();
      AddressFunding = snapshotAnchorer.GetProperty("addressFunding").GetString();
    }
  }
}
