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
    public partial string HintSync { get; set; }

    [ObservableProperty]
    public partial string AgeHeaderTipParent { get; set; }

    [ObservableProperty]
    public partial long Funds { get; set; }


    public void Update(JsonElement snapshotMiner, bool isAccountsAtTip)
    {
      StatusMining = snapshotMiner.GetProperty("isMining").GetBoolean()
        ? $"Mining with a budget of {snapshotMiner.GetProperty("budget").GetInt64()} satoshis per 10 minutes, anchor fee {snapshotMiner.GetProperty("feeAnchor").GetInt64()}"
        : "Stopped";

      StateRoundLast = snapshotMiner.GetProperty("stateRoundLast").GetString();
      Funds = snapshotMiner.GetProperty("funds").GetInt64();

      string nameTokenParent = snapshotMiner.GetProperty("nameTokenParent").GetString();

      AgeHeaderTipParent = $"The {nameTokenParent} header tip is {snapshotMiner.GetProperty("secondsAgeTipParent").GetInt64() / 60} minutes old.";

      if (!isAccountsAtTip)
        HintSync = "Probably not synced: the accounts are not at the tip.";
      else if (!snapshotMiner.GetProperty("isBlockTipAtHeaderTip").GetBoolean())
        HintSync = $"Probably not synced: the {nameTokenParent} blocks are not at the {nameTokenParent} header tip.";
      else if (!snapshotMiner.GetProperty("isPeerConnected").GetBoolean())
        HintSync = $"Probably not synced: no {nameTokenParent} peer is connected.";
      else
        HintSync = "Probably synced.";
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
