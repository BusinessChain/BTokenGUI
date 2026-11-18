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
    public partial int CountAnchorsPer10Minutes { get; set; }

    [ObservableProperty]
    public partial long FeeAnchor { get; set; }

    [ObservableProperty]
    public partial string StatusMining { get; set; }

    [ObservableProperty]
    public partial string StateRoundLast { get; set; }


    public void Update(JsonElement snapshotMiner)
    {
      StatusMining = snapshotMiner.GetProperty("isMining").GetBoolean()
        ? $"Mining {snapshotMiner.GetProperty("countAnchorsPer10Minutes").GetInt32()} anchors per 10 minutes at anchor fee {snapshotMiner.GetProperty("feeAnchor").GetInt64()} satoshis"
        : "Stopped";

      StateRoundLast = snapshotMiner.GetProperty("stateRoundLast").GetString();
    }

    [RelayCommand]
    async Task StartMining()
    {
      await SendRequestMiner("startMining", new Dictionary<string, object>
      {
        ["countAnchorsPer10Minutes"] = CountAnchorsPer10Minutes,
        ["feeAnchor"] = FeeAnchor
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
