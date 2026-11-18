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


    public void Update(JsonElement snapshotMiner)
    {
      StatusMining = snapshotMiner.GetProperty("isMining").GetBoolean()
        ? $"Mining with a budget of {snapshotMiner.GetProperty("budget").GetInt64()} satoshis per 10 minutes"
        : "Stopped";

      StateRoundLast = snapshotMiner.GetProperty("stateRoundLast").GetString();
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
