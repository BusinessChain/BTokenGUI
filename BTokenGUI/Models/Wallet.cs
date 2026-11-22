using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using BTokenSigner;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BTokenGUI.Models;

public partial class Wallet : ObservableObject
{
  [ObservableProperty]
  public partial string Address { get; set; }

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

  [ObservableProperty]
  public partial string StateBackupLast { get; set; }

  string PathKey;
  string KeyPrivate;
  byte[] KeyPublic;
  ILiteCollection<BsonDocument> TXsStored;
  ILiteCollection<BsonDocument> StateStored;
  string HashTipLast;

  const string ID_HEIGHT_SCANNED_LAST = "heightScannedLast";
  const string NAME_FIELD_VALUE = "value";


  public Wallet()
  {
    string directoryWallet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BToken", "WalletGUI");
    PathKey = Path.Combine(directoryWallet, "key");

    Directory.CreateDirectory(directoryWallet);

    if (!File.Exists(PathKey))
      File.WriteAllText(PathKey, Crypto.GenerateKeyPrivate() + Environment.NewLine);

    SetKeyPrivate(LoadKeysPrivate().Last());

    LiteDatabase liteDatabase = new(Path.Combine(directoryWallet, "wallet.db"));
    AppDomain.CurrentDomain.ProcessExit += (s, e) => { liteDatabase.Dispose(); };

    TXsStored = liteDatabase.GetCollection<BsonDocument>("transactions");
    StateStored = liteDatabase.GetCollection<BsonDocument>("state");
  }

  List<string> LoadKeysPrivate()
  {
    return File.ReadAllLines(PathKey).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
  }

  void SetKeyPrivate(string keyPrivate)
  {
    KeyPrivate = keyPrivate;
    KeyPublic = Crypto.GetKeyPublic(KeyPrivate);
    Address = Crypto.ComputeHash160(KeyPublic).BinaryToBase58Check();
  }

  [RelayCommand]
  async Task SendPayment()
  {
    try
    {
      JsonElement tXUnsigned = await Node.SendRequest("createTXBTokenUnsigned", new Dictionary<string, object>
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

  internal async Task WriteBackup(Stream stream, string nameFile)
  {
    try
    {
      JsonElement keysMiner = await Node.SendRequest("getKeysMiner", null);

      await JsonSerializer.SerializeAsync(stream, new Dictionary<string, object>
      {
        ["wallet"] = KeyPrivate,
        ["minerBitcoin"] = keysMiner.GetProperty("bitcoin"),
        ["minerBToken"] = keysMiner.GetProperty("bToken")
      }, new JsonSerializerOptions { WriteIndented = true });

      StateBackupLast = $"Wrote the backup of all keys to {nameFile}.";
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException)
    {
      StateBackupLast = exception.Message;
    }
  }

  internal async Task ImportBackup(string backup, string nameFile)
  {
    try
    {
      using JsonDocument document = JsonDocument.Parse(backup);
      JsonElement root = document.RootElement;

      await Node.SendRequest("importKeysMiner", new Dictionary<string, object>
      {
        ["bitcoin"] = root.GetProperty("minerBitcoin"),
        ["bToken"] = root.GetProperty("minerBToken")
      });

      string keyWallet = root.GetProperty("wallet").GetString();

      if (keyWallet == KeyPrivate)
      {
        StateBackupLast = $"Imported the Miner's keys from {nameFile}.";
        return;
      }

      List<string> keysPrivate = LoadKeysPrivate();
      keysPrivate.Remove(keyWallet);
      keysPrivate.Add(keyWallet);

      File.WriteAllLines(PathKey, keysPrivate);
      SetKeyPrivate(keyWallet);

      StateStored.Upsert(new BsonDocument { ["_id"] = ID_HEIGHT_SCANNED_LAST, [NAME_FIELD_VALUE] = 0 });
      HashTipLast = null;

      StateBackupLast = $"Imported all keys from {nameFile}.";
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException or KeyNotFoundException or IOException)
    {
      StateBackupLast = exception.Message;
    }
  }

  internal async Task UpdateBalance()
  {
    JsonElement balance = await Node.SendRequest("getBalanceAccount", new Dictionary<string, object> { ["address"] = Address }).ConfigureAwait(false);

    Dispatcher.UIThread.Post(() =>
      Balance = balance.ValueKind == JsonValueKind.Null ? "unknown while the accounts synchronize" : balance.GetInt64().ToString());
  }

  internal async Task ProcessTip(string hashTip, int heightTip)
  {
    if (hashTip == HashTipLast)
      return;

    int heightScannedLast = Math.Min(StateStored.FindById(ID_HEIGHT_SCANNED_LAST)?[NAME_FIELD_VALUE].AsInt32 ?? heightTip, heightTip);

    for (int height = heightScannedLast + 1; height <= heightTip; height++)
    {
      JsonElement blockTXs = await Node.SendRequest("getTXsAccount", new Dictionary<string, object> { ["address"] = Address, ["height"] = height }).ConfigureAwait(false);

      if (blockTXs.ValueKind != JsonValueKind.Null)
        foreach (JsonElement tX in blockTXs.GetProperty("tXs").EnumerateArray())
        {
          BsonDocument document = TXsStored.FindById(tX.GetProperty("hash").GetString()) ?? new BsonDocument
          {
            ["_id"] = tX.GetProperty("hash").GetString(),
            ["addressSource"] = tX.GetProperty("addressSource").GetString(),
            ["fee"] = tX.GetProperty("fee").GetInt64(),
            ["outputs"] = new BsonArray(tX.GetProperty("outputs").EnumerateArray().Select(o => new BsonDocument
            {
              ["address"] = o.GetProperty("address").GetString(),
              ["value"] = o.GetProperty("value").GetInt64()
            }))
          };

          document["height"] = height;
          TXsStored.Upsert(document);
        }

      heightScannedLast = height;
    }

    StateStored.Upsert(new BsonDocument { ["_id"] = ID_HEIGHT_SCANNED_LAST, [NAME_FIELD_VALUE] = heightScannedLast });

    JsonElement account = await Node.SendRequest("getAccount", new Dictionary<string, object> { ["address"] = Address }).ConfigureAwait(false);

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
    await Node.SendRequest("submitTXBTokenSigned", new Dictionary<string, object>
    {
      ["tXUnsigned"] = tXUnsigned,
      ["signature"] = signature
    });
  }
}
