using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BTokenGUI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public partial class Miner : ObservableObject, INotifyDataErrorInfo
  {
    static readonly Dictionary<string, Dictionary<string, string>> PropertiesParameterByMethod = new()
    {
      ["startMining"] = new() { ["budget"] = nameof(Budget) },
      ["withdrawBitcoin"] = new()
      {
        ["address"] = nameof(AddressWithdrawal),
        ["fee"] = nameof(FeeWithdrawal)
      },
      ["withdrawRevenueMiner"] = new()
      {
        ["address"] = nameof(AddressRevenue),
        ["fee"] = nameof(FeeRevenue)
      }
    };

    Dictionary<string, string> ErrorsProperty = [];

    public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

    [ObservableProperty]
    public partial string ErrorWithdrawal { get; set; }

    [ObservableProperty]
    public partial decimal? Budget { get; set; }

    [ObservableProperty]
    public partial string StatusMining { get; set; }

    [ObservableProperty]
    public partial string StateRoundLast { get; set; }

    [ObservableProperty]
    public partial long Funds { get; set; }

    [ObservableProperty]
    public partial string Revenue { get; set; }

    [ObservableProperty]
    public partial string AddressRevenue { get; set; }

    [ObservableProperty]
    public partial decimal? FeeRevenue { get; set; }

    [ObservableProperty]
    public partial string AddressFunding { get; set; }

    [ObservableProperty]
    public partial string AddressWithdrawal { get; set; }

    [ObservableProperty]
    public partial decimal? FeeWithdrawal { get; set; }

    [ObservableProperty]
    public partial string StateWithdrawalLast { get; set; }


    public bool HasErrors
    {
      get
      {
        return ErrorsProperty.Count > 0;
      }
    }

    public IEnumerable GetErrors(string propertyName)
    {
      if (propertyName != null && ErrorsProperty.TryGetValue(propertyName, out string error))
        return new[] { error };

      return Array.Empty<string>();
    }

    internal bool MarkErrors(string method, Exception exception)
    {
      foreach (string parameter in (exception as RPCException)?.Parameters ?? [])
        if (PropertiesParameterByMethod.GetValueOrDefault(method)?.TryGetValue(parameter, out string property) == true)
        {
          ErrorsProperty[property] = exception.Message;
          ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
        }

      return ErrorsProperty.Count > 0;
    }

    internal void ClearErrors()
    {
      ErrorWithdrawal = null;

      List<string> properties = [.. ErrorsProperty.Keys];
      ErrorsProperty.Clear();

      properties.ForEach(p => ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(p)));
    }

    public void Update(JsonElement snapshotMiner)
    {
      StatusMining = snapshotMiner.GetProperty("isMining").GetBoolean()
        ? $"Mining with a budget of {snapshotMiner.GetProperty("budget").GetInt64()} satoshis per 10 minutes, anchor fee {snapshotMiner.GetProperty("feeAnchor").GetInt64()}"
        : "Stopped";

      StateRoundLast = snapshotMiner.GetProperty("stateRoundLast").GetString();
      Funds = snapshotMiner.GetProperty("funds").GetInt64();

      JsonElement revenue = snapshotMiner.GetProperty("revenue");
      Revenue = revenue.ValueKind == JsonValueKind.Null ? "unknown while the accounts synchronize" : revenue.GetInt64().ToString();

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
      ClearErrors();

      try
      {
        await MainViewModel.SendRequest(method, parameters);
      }
      catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
      {
        if (!MarkErrors(method, exception))
          StateRoundLast = exception.Message;
      }
    }
  }
}
