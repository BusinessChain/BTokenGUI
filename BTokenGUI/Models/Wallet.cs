using System;
using System.IO;
using BTokenSigner;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Wallet : ObservableObject
{
  public string Address { get; }

  [ObservableProperty]
  public partial string Balance { get; set; }


  public Wallet()
  {
    string pathKey = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WalletGUI", "key");

    if (!File.Exists(pathKey))
    {
      Directory.CreateDirectory(Path.GetDirectoryName(pathKey));
      File.WriteAllText(pathKey, Crypto.GenerateKeyPrivate());
    }

    string keyPrivate = File.ReadAllText(pathKey).Trim();

    Address = Crypto.ComputeHash160(Crypto.GetKeyPublic(keyPrivate)).BinaryToBase58Check();
  }
}
