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
    ["budget"] = nameof(Budget),
    ["level"] = nameof(LevelWithdrawalAuto)
  };

  const int COUNT_PERIODS_BUDGET_DAY = 144;

  Dictionary<string, string> ErrorsProperty = [];

  long? LevelWithdrawalAutoNode;

  long BudgetNode;

  Models.Wallet WalletGUI;

  public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

  public Wallet WalletBitcoin { get; }

  public Wallet WalletBToken { get; }

  [ObservableProperty]
  public partial decimal? Budget { get; set; }

  [ObservableProperty]
  public partial double FillingConverter { get; set; }

  [ObservableProperty]
  public partial bool IsMining { get; set; }

  [ObservableProperty]
  public partial bool IsFormConverterOpen { get; set; }

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

  [ObservableProperty]
  public partial bool IsWithdrawalAutoChosen { get; set; }

  [ObservableProperty]
  public partial decimal? LevelWithdrawalAuto { get; set; }


  public Mining(Models.Wallet walletGUI)
  {
    WalletGUI = walletGUI;
    WalletBitcoin = new(this, "withdrawBitcoin", "Bitcoin tank", isAutoOffered: false, isFeedingConverter: true);
    WalletBToken = new(this, "withdrawRevenueMiner", "BToken tank", isAutoOffered: true, isFeedingConverter: false);
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
    BudgetNode = snapshotMiner.GetProperty("budget").GetInt64();
    long scaleBitcoin = COUNT_PERIODS_BUDGET_DAY * BudgetNode;
    WalletBitcoin.Filling = fundsBitcoin == 0 ? 0 : (double)fundsBitcoin / (fundsBitcoin + scaleBitcoin);

    if (scaleBitcoin == 0)
      FillingConverter = 0;
    else if (fundsBitcoin == 0)
      FillingConverter = 1;
    else
      FillingConverter = Math.Min(1, (double)scaleBitcoin / fundsBitcoin);

    JsonElement levelWithdrawalAuto = snapshotMiner.GetProperty("levelWithdrawalAuto");
    LevelWithdrawalAutoNode = levelWithdrawalAuto.ValueKind == JsonValueKind.Null ? null : levelWithdrawalAuto.GetInt64();
    WalletBToken.IsAutoActive = LevelWithdrawalAutoNode != null;
    WalletBToken.IsAutoToWalletGUI = WalletGUI.AddressWithdrawalAuto == WalletGUI.Address;

    if (WalletBToken.Funds is not long fundsBToken || fundsBToken == 0)
      WalletBToken.Filling = 0;
    else if (LevelWithdrawalAutoNode is not long level)
      WalletBToken.Filling = 1;
    else
      WalletBToken.Filling = Math.Min(1, (double)fundsBToken / level);
  }

  [RelayCommand]
  async Task ToggleMining()
  {
    if (IsMining)
      await SendRequestMiner("stopMining", null);
    else
      await SendRequestMiner("startMining", new Dictionary<string, object>
      {
        ["budget"] = Budget is decimal budget ? Math.Round(budget / COUNT_PERIODS_BUDGET_DAY) : null
      });
  }

  [RelayCommand]
  void OpenFormConverter()
  {
    ClearErrors();

    if (BudgetNode > 0)
      Budget = COUNT_PERIODS_BUDGET_DAY * BudgetNode;

    IsFormConverterOpen = true;
  }

  [RelayCommand]
  void CloseFormConverter()
  {
    IsFormConverterOpen = false;
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
    IsWithdrawalAutoChosen = wallet.IsAutoActive;
    LevelWithdrawalAuto = LevelWithdrawalAutoNode;
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
    await SubmitWithdrawal(true);
  }

  [RelayCommand]
  async Task ApplyWithdrawalAuto()
  {
    await SubmitWithdrawal(false);
  }

  async Task SubmitWithdrawal(bool isSending)
  {
    ClearErrors();

    try
    {
      if (IsWithdrawalAutoChosen)
      {
        await Node.SendRequest("setWithdrawalAutoMiner", new Dictionary<string, object>
        {
          ["address"] = AddressWithdrawal,
          ["fee"] = FeeWithdrawal,
          ["level"] = LevelWithdrawalAuto
        });

        WalletGUI.AddressWithdrawalAuto = AddressWithdrawal;
      }
      else
      {
        if (WalletWithdrawal.IsAutoActive)
          await Node.SendRequest("stopWithdrawalAutoMiner", null);

        if (isSending)
          await Node.SendRequest(WalletWithdrawal.MethodWithdrawal, new Dictionary<string, object>
          {
            ["address"] = AddressWithdrawal,
            ["fee"] = FeeWithdrawal
          });
      }

      WalletWithdrawal = null;
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
    {
      if (!MarkErrors(exception))
        ErrorWithdrawal = exception.Message;
    }
  }
}
