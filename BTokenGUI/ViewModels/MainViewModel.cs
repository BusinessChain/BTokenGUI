using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using BTokenGUI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTokenGUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
  [ObservableProperty]
  public partial string StatusConnection { get; set; } = "Connecting to node";

  [ObservableProperty]
  public partial Blockchain.Miner MinerWithdrawal { get; set; }

  public ObservableCollection<Network> Networks { get; } = [];

  public ObservableCollection<Blockchain> Blockchains { get; } = [];

  public Wallet Wallet { get; } = new();

  const string URL_RPC = "http://127.0.0.1:8778/";
  const int MILLISECONDS_INTERVAL_POLL_NODE = 1000;
  const int COUNT_BLOCKS_NEWEST = 6;

  static HttpClient HttpClient = new();


  public MainViewModel()
  {
    _ = PollNode();
  }

  async Task PollNode()
  {
    while (true)
    {
      await Task.Delay(MILLISECONDS_INTERVAL_POLL_NODE).ConfigureAwait(false);

      try
      {
        JsonElement snapshotState = await SendRequest("getSnapshotState", new Dictionary<string, object> { ["countBlocks"] = COUNT_BLOCKS_NEWEST }).ConfigureAwait(false);
        JsonElement networksJSON = snapshotState.GetProperty("networks");
        JsonElement blockchainsJSON = snapshotState.GetProperty("blockchains");

        Dispatcher.UIThread.Post(() =>
        {
          foreach (JsonProperty networkJSON in networksJSON.EnumerateObject())
          {
            Network network = Networks.FirstOrDefault(n => n.Name == networkJSON.Name);

            if (network == null)
            {
              network = new Network(networkJSON.Name);
              Networks.Add(network);
            }

            network.Update(networkJSON.Value);
          }

          foreach (JsonProperty blockchainJSON in blockchainsJSON.EnumerateObject())
          {
            Blockchain blockchain = Blockchains.FirstOrDefault(b => b.Name == blockchainJSON.Name);

            if (blockchain == null)
            {
              blockchain = new Blockchain(blockchainJSON.Name);
              Blockchains.Add(blockchain);
            }

            blockchain.Update(blockchainJSON.Value);
          }

          StatusConnection = "Connected to node";
        });

        JsonElement balance = await SendRequest("getBalanceAccount", new Dictionary<string, object> { ["address"] = Wallet.Address }).ConfigureAwait(false);

        Dispatcher.UIThread.Post(() =>
          Wallet.Balance = balance.ValueKind == JsonValueKind.Null ? "unknown while the accounts synchronize" : balance.GetInt64().ToString());

        JsonElement headerTipBToken = blockchainsJSON.GetProperty("BToken").GetProperty("headers")[0];

        await Wallet.ProcessTip(headerTipBToken.GetProperty("hash").GetString(), headerTipBToken.GetProperty("height").GetInt32()).ConfigureAwait(false);
      }
      catch (Exception exception)
      {
        if (exception is not (HttpRequestException or TaskCanceledException or InvalidOperationException))
          Trace.WriteLine($"PollNode failed with {exception.GetType().Name}: {exception.Message}");

        Dispatcher.UIThread.Post(() => StatusConnection = "Connecting to node");
      }
    }
  }

  [RelayCommand]
  void OpenWithdrawal(Blockchain.Miner miner)
  {
    miner.ClearErrorsWithdrawal();
    MinerWithdrawal = miner;
  }

  [RelayCommand]
  void CancelWithdrawal()
  {
    MinerWithdrawal = null;
  }

  [RelayCommand]
  async Task WithdrawBitcoin()
  {
    Blockchain.Miner miner = MinerWithdrawal;

    miner.ClearErrorsWithdrawal();

    try
    {
      JsonElement withdrawal = await SendRequest("withdrawBitcoin", new Dictionary<string, object>
      {
        ["address"] = miner.AddressWithdrawal,
        ["value"] = miner.ValueWithdrawal,
        ["fee"] = miner.FeeWithdrawal
      });

      miner.StateWithdrawalLast = $"Sent withdrawal {withdrawal.GetProperty("hash").GetString()}.";
      MinerWithdrawal = null;
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
    {
      miner.MarkErrorsWithdrawal(exception.Message, (exception as RPCException)?.Parameters ?? []);
    }
  }

  internal static async Task<JsonElement> SendRequest(string method, object parameters)
  {
    byte[] request = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
    {
      ["jsonrpc"] = "2.0",
      ["id"] = 1,
      ["method"] = method,
      ["params"] = parameters
    });

    using HttpResponseMessage response = await HttpClient.PostAsync(URL_RPC, new ByteArrayContent(request)).ConfigureAwait(false);

    using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false));

    JsonElement root = document.RootElement;

    if (root.TryGetProperty("error", out JsonElement error))
    {
      List<string> parametersFaulty = [];

      if (error.TryGetProperty("data", out JsonElement data) && data.TryGetProperty("parameters", out JsonElement parametersJSON))
        parametersFaulty = parametersJSON.EnumerateArray().Select(p => p.GetString()).ToList();

      throw new RPCException(error.GetProperty("message").GetString(), parametersFaulty);
    }

    return root.GetProperty("result").Clone();
  }
}
