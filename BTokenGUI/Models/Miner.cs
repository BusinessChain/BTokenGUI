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
  public partial class Miner : ObservableObject
  {
    [ObservableProperty]
    public partial long Budget { get; set; }

    [ObservableProperty]
    public partial string StatusMining { get; set; }

    [ObservableProperty]
    public partial string StateRoundLast { get; set; }

    [ObservableProperty]
    public partial long Funds { get; set; }

    [ObservableProperty]
    public partial string AddressFunding { get; set; }

    [ObservableProperty]
    public partial string AddressWithdrawal { get; set; }

    [ObservableProperty]
    public partial long ValueWithdrawal { get; set; }

    [ObservableProperty]
    public partial long FeeWithdrawal { get; set; }

    [ObservableProperty]
    public partial string StateWithdrawalLast { get; set; }


    public void Update(JsonElement snapshotMiner)
    {
      StatusMining = snapshotMiner.GetProperty("isMining").GetBoolean()
        ? $"Mining with a budget of {snapshotMiner.GetProperty("budget").GetInt64()} satoshis per 10 minutes, anchor fee {snapshotMiner.GetProperty("feeAnchor").GetInt64()}"
        : "Stopped";

      StateRoundLast = snapshotMiner.GetProperty("stateRoundLast").GetString();
      Funds = snapshotMiner.GetProperty("funds").GetInt64();
      AddressFunding = snapshotMiner.GetProperty("addressFunding").GetString();
    }

    [RelayCommand]
    async Task StartMining()
    {
      await SendRequestMiner("startMining", new Dictionary<string, object>
      {
        ["budget"] = Budget
      });
    }

    [RelayCommand]
    async Task StopMining()
    {
      await SendRequestMiner("stopMining", null);
    }

    async Task SendRequestMiner(string method, object parameters)
    {
      try
      {
        await MainViewModel.SendRequest(method, parameters);
      }
      catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
      {
        StateRoundLast = exception.Message;
      }
    }
  }
}
