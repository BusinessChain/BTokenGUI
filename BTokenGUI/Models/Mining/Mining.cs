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

  const int COUNT_PERIODS_BUDGET_DAY = 144;

  Dictionary<string, string> ErrorsProperty = [];

  public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

  public Wallet WalletBitcoin { get; }

  public Wallet WalletBToken { get; }

  [ObservableProperty]
  public partial decimal? Budget { get; set; }

  [ObservableProperty]
  public partial bool IsMining { get; set; }

  [ObservableProperty]
  public partial string StateMiner { get; set; }

  [ObservableProperty]
  public partial string AddressWithdrawal { get; set; }

  [ObservableProperty]
  public partial decimal? FeeWithdrawal { get; set; }

  [ObservableProperty]
  public partial string ErrorWithdrawal { get; set; }

  [ObservableProperty]
  public partial Wallet WalletWithdrawal { get; set; }


  public Mining()
  {
    WalletBitcoin = new(this, "withdrawBitcoin", "Withdraw bitcoin");
    WalletBToken = new(this, "withdrawRevenueMiner", "Withdraw Miner revenue");
  }

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
    WalletBitcoin.Funds = snapshotAnchorer.GetProperty("funds").GetInt64();
    WalletBitcoin.Address = snapshotAnchorer.GetProperty("addressFunding").GetString();

    IsMining = snapshotMiner.GetProperty("isMining").GetBoolean();
    StateMiner = snapshotMiner.GetProperty("stateMiner").GetString();

    JsonElement revenue = snapshotMiner.GetProperty("revenue");
    WalletBToken.Funds = revenue.ValueKind == JsonValueKind.Null ? null : revenue.GetInt64();

    long fundsBitcoin = WalletBitcoin.Funds.Value;
    long scaleBitcoin = COUNT_PERIODS_BUDGET_DAY * snapshotMiner.GetProperty("budget").GetInt64();
    WalletBitcoin.Filling = fundsBitcoin == 0 ? 0 : (double)fundsBitcoin / (fundsBitcoin + scaleBitcoin);

    JsonElement levelWithdrawalAuto = snapshotMiner.GetProperty("levelWithdrawalAuto");

    if (WalletBToken.Funds is not long fundsBToken || fundsBToken == 0)
      WalletBToken.Filling = 0;
    else if (levelWithdrawalAuto.ValueKind == JsonValueKind.Null)
      WalletBToken.Filling = 1;
    else
      WalletBToken.Filling = Math.Min(1, (double)fundsBToken / levelWithdrawalAuto.GetInt64());
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

  void OpenFormWithdrawal(Wallet wallet)
  {
    ClearErrors();
    WalletWithdrawal = wallet;
  }

  [RelayCommand]
  void CancelWithdrawal()
  {
    WalletWithdrawal = null;
  }

  [RelayCommand]
  async Task Withdraw()
  {
    ClearErrors();

    try
    {
      await Node.SendRequest(WalletWithdrawal.MethodWithdrawal, new Dictionary<string, object>
      {
        ["address"] = AddressWithdrawal,
        ["fee"] = FeeWithdrawal
      });

      WalletWithdrawal = null;
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
    {
      if (!MarkErrors(exception))
        ErrorWithdrawal = exception.Message;
    }
  }
}
