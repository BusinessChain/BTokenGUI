using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTokenGUI.Models;

public partial class Mining : ObservableObject, INotifyDataErrorInfo
{
  static readonly Dictionary<string, string> PropertiesParameter = new()
  {
    ["address"] = nameof(AddressWithdrawal),
    ["fee"] = nameof(FeeWithdrawal),
    ["budget"] = nameof(Budget)
  };

  Dictionary<string, string> ErrorsProperty = [];

  public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

  [ObservableProperty]
  public partial long Funds { get; set; }

  [ObservableProperty]
  public partial string AddressFunding { get; set; }

  [ObservableProperty]
  public partial decimal? Budget { get; set; }

  [ObservableProperty]
  public partial bool IsMining { get; set; }

  [ObservableProperty]
  public partial string StateMiner { get; set; }

  [ObservableProperty]
  public partial string Revenue { get; set; }

  [ObservableProperty]
  public partial string AddressWithdrawal { get; set; }

  [ObservableProperty]
  public partial decimal? FeeWithdrawal { get; set; }

  [ObservableProperty]
  public partial string ErrorWithdrawal { get; set; }

  [ObservableProperty]
  public partial string MethodWithdrawal { get; set; }

  [ObservableProperty]
  public partial string TitleWithdrawal { get; set; }

  [ObservableProperty]
  public partial string BalanceWithdrawal { get; set; }

  [ObservableProperty]
  public partial bool IsWithdrawalOpen { get; set; }


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

  bool MarkErrors(Exception exception)
  {
    foreach (string parameter in (exception as RPCException)?.Parameters ?? [])
      if (PropertiesParameter.TryGetValue(parameter, out string property))
      {
        ErrorsProperty[property] = exception.Message;
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
      }

    return ErrorsProperty.Count > 0;
  }

  void ClearErrors()
  {
    ErrorWithdrawal = null;

    List<string> properties = [.. ErrorsProperty.Keys];
    ErrorsProperty.Clear();

    properties.ForEach(p => ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(p)));
  }

  public void Update(JsonElement snapshotAnchorer, JsonElement snapshotMiner)
  {
    Funds = snapshotAnchorer.GetProperty("funds").GetInt64();
    AddressFunding = snapshotAnchorer.GetProperty("addressFunding").GetString();

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
      await Node.SendRequest(method, parameters);
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
    {
      if (!MarkErrors(exception))
        StateMiner = exception.Message;
    }
  }

  [RelayCommand]
  void OpenWithdrawal()
  {
    OpenFormWithdrawal("withdrawBitcoin", "Withdraw bitcoin", $"Funds: {Funds}");
  }

  [RelayCommand]
  void OpenWithdrawalRevenue()
  {
    OpenFormWithdrawal("withdrawRevenueMiner", "Withdraw Miner revenue", $"Revenue: {Revenue}");
  }

  void OpenFormWithdrawal(string method, string title, string balance)
  {
    ClearErrors();
    MethodWithdrawal = method;
    TitleWithdrawal = title;
    BalanceWithdrawal = balance;
    IsWithdrawalOpen = true;
  }

  [RelayCommand]
  void CancelWithdrawal()
  {
    IsWithdrawalOpen = false;
  }

  [RelayCommand]
  async Task Withdraw()
  {
    ClearErrors();

    try
    {
      await Node.SendRequest(MethodWithdrawal, new Dictionary<string, object>
      {
        ["address"] = AddressWithdrawal,
        ["fee"] = FeeWithdrawal
      });

      IsWithdrawalOpen = false;
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
    {
      if (!MarkErrors(exception))
        ErrorWithdrawal = exception.Message;
    }
  }
}
