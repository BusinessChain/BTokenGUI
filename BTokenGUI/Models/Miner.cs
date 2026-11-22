using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BTokenGUI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public partial class Miner : OwnerWallet
  {
    [ObservableProperty]
    public partial decimal? Budget { get; set; }

    [ObservableProperty]
    public partial bool IsMining { get; set; }

    [ObservableProperty]
    public partial string StateMiner { get; set; }

    [ObservableProperty]
    public partial string Revenue { get; set; }


    public Miner()
    {
      PropertiesParameter["budget"] = nameof(Budget);
    }

    public void Update(JsonElement snapshotMiner)
    {
      IsMining = snapshotMiner.GetProperty("isMining").GetBoolean();
      StateMiner = snapshotMiner.GetProperty("stateMiner").GetString();

      JsonElement revenue = snapshotMiner.GetProperty("revenue");
      Revenue = revenue.ValueKind == JsonValueKind.Null ? "unknown while the accounts synchronize" : revenue.GetInt64().ToString();
    }

    [RelayCommand]
    async Task ToggleMining()
    {
      if (IsMining)
        await SendRequestMiner("stopMining", null);
      else
        await SendRequestMiner("startMining", new Dictionary<string, object>
        {
          ["budget"] = Budget
        });
    }

    async Task SendRequestMiner(string method, object parameters)
    {
      ClearErrors();

      try
      {
        await MainViewModel.SendRequest(method, parameters);
      }
      catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
      {
        if (!MarkErrors(exception))
          StateMiner = exception.Message;
      }
    }
  }
}
