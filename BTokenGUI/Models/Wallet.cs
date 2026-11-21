using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BTokenGUI.ViewModels;
using BTokenSigner;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;

namespace BTokenGUI.Models;

public partial class Wallet : ObservableObject
{
  public string Address { get; }

  [ObservableProperty]
  public partial string Balance { get; set; }

  [ObservableProperty]
  public partial string AddressPayee { get; set; }

  [ObservableProperty]
  public partial long ValuePayment { get; set; }

  [ObservableProperty]
  public partial long FeePayment { get; set; }

  [ObservableProperty]
  public partial string StatePaymentLast { get; set; }

  string KeyPrivate;
  byte[] KeyPublic;
  ILiteCollection<BsonDocument> TXsStored;
  string HashTipLast;


  public Wallet()
  {
    string directoryWallet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WalletGUI");
    string pathKey = Path.Combine(directoryWallet, "key");

    Directory.CreateDirectory(directoryWallet);

    if (!File.Exists(pathKey))
      File.WriteAllText(pathKey, Crypto.GenerateKeyPrivate());

    KeyPrivate = File.ReadAllText(pathKey).Trim();
    KeyPublic = Crypto.GetKeyPublic(KeyPrivate);
    Address = Crypto.ComputeHash160(KeyPublic).BinaryToBase58Check();

    LiteDatabase liteDatabase = new(Path.Combine(directoryWallet, "wallet.db"));
    AppDomain.CurrentDomain.ProcessExit += (s, e) => { liteDatabase.Dispose(); };

    TXsStored = liteDatabase.GetCollection<BsonDocument>("transactions");
  }

  [RelayCommand]
  async Task SendPayment()
  {
    try
    {
      JsonElement tXUnsigned = await MainViewModel.SendRequest("createTXBTokenUnsigned", new Dictionary<string, object>
      {
        ["keyPublic"] = Convert.ToHexString(KeyPublic),
        ["fee"] = FeePayment,
        ["outputs"] = new[] { new Dictionary<string, object> { ["address"] = AddressPayee, ["value"] = ValuePayment } }
      });

      string hash = tXUnsigned.GetProperty("hash").GetString();
      string signature = Convert.ToHexString(Crypto.GetSignature(KeyPrivate, Convert.FromHexString(tXUnsigned.GetProperty("dataToBeSigned").GetString())));

      await SubmitPayment(tXUnsigned.GetProperty("tXUnsigned").GetString(), signature);

      TXsStored.Insert(new BsonDocument
      {
        ["_id"] = hash,
        ["addressSource"] = Address,
        ["fee"] = FeePayment,
        ["outputs"] = new BsonArray { new BsonDocument { ["address"] = AddressPayee, ["value"] = ValuePayment } },
        ["height"] = 0,
        ["tXUnsigned"] = tXUnsigned.GetProperty("tXUnsigned").GetString(),
        ["signature"] = signature,
        ["nonce"] = tXUnsigned.GetProperty("nonce").GetInt32(),
        ["heightAccountCreated"] = tXUnsigned.GetProperty("heightAccountCreated").GetInt32()
      });

      StatePaymentLast = $"Sent payment {hash}.";
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
    {
      StatePaymentLast = exception.Message;
    }
  }

  internal async Task ProcessTip(string hashTip)
  {
    if (hashTip == HashTipLast)
      return;

    JsonElement account = await MainViewModel.SendRequest("getAccount", new Dictionary<string, object> { ["address"] = Address }).ConfigureAwait(false);

    if (account.ValueKind != JsonValueKind.Null)
      foreach (BsonDocument payment in TXsStored.Find(Query.And(
        Query.EQ("heightAccountCreated", account.GetProperty("heightAccountCreated").GetInt32()),
        Query.GTE("nonce", account.GetProperty("nonce").GetInt32()))))
        try
        {
          await SubmitPayment(payment["tXUnsigned"].AsString, payment["signature"].AsString).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        { }

    HashTipLast = hashTip;
  }

  static async Task SubmitPayment(string tXUnsigned, string signature)
  {
    await MainViewModel.SendRequest("submitTXBTokenSigned", new Dictionary<string, object>
    {
      ["tXUnsigned"] = tXUnsigned,
      ["signature"] = signature
    });
  }
}
