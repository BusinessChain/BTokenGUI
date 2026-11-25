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

namespace BTokenGUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
  [ObservableProperty]
  public partial string StatusConnection { get; set; } = "Connecting to node";

  public ObservableCollection<Network> Networks { get; } = [];

  public Blockchain BlockchainBitcoin { get; }

  public Blockchain BlockchainBToken { get; }

  public Mining Mining { get; }

  public Wallet Wallet { get; } = new();

  const int MILLISECONDS_INTERVAL_POLL_NODE = 1000;
  const int COUNT_BLOCKS_NEWEST = 3;
  const int COUNT_TXS_POOL = 15;


  public MainViewModel()
  {
    Mining = new(Wallet);
    BlockchainBitcoin = new("Bitcoin", blockchainParent: null, tank: Mining.WalletBitcoin, flights: Mining.Flights);
    BlockchainBToken = new("BToken", blockchainParent: BlockchainBitcoin, tank: Mining.WalletBToken, flights: Mining.Flights);

    _ = PollNode();
  }

  async Task PollNode()
  {
    while (true)
    {
      await Task.Delay(MILLISECONDS_INTERVAL_POLL_NODE).ConfigureAwait(false);

      try
      {
        JsonElement snapshotState = await Node.SendRequest("getSnapshotState", new Dictionary<string, object> { ["countBlocks"] = COUNT_BLOCKS_NEWEST, ["countTXsPool"] = COUNT_TXS_POOL }).ConfigureAwait(false);
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

          JsonElement snapshotBitcoin = blockchainsJSON.GetProperty("Bitcoin");
          JsonElement snapshotBToken = blockchainsJSON.GetProperty("BToken");

          Mining.Update(snapshotBitcoin.GetProperty("anchorer"), snapshotBToken.GetProperty("miner"));
          BlockchainBitcoin.Update(snapshotBitcoin);
          BlockchainBToken.Update(snapshotBToken);
          StatusConnection = "Connected to node";
        });

        await Wallet.UpdateBalance().ConfigureAwait(false);

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
}
