using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BTokenGUI.ViewModels;

namespace BTokenGUI.Views;

public partial class WalletView : UserControl
{
  public WalletView()
  {
    InitializeComponent();
  }

  async void BackupKeys(object sender, RoutedEventArgs e)
  {
    IStorageFile file = await TopLevel.GetTopLevel(this).StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    {
      Title = "Backup of all keys",
      SuggestedFileName = "BTokenKeys.json"
    });

    if (file == null)
      return;

    await using Stream stream = await file.OpenWriteAsync();

    await ((MainViewModel)DataContext).Wallet.WriteBackup(stream, file.Name);
  }

  async void ImportKeys(object sender, RoutedEventArgs e)
  {
    IReadOnlyList<IStorageFile> files = await TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
    {
      Title = "Import a backup of all keys",
      AllowMultiple = false
    });

    if (files.Count == 0)
      return;

    await using Stream stream = await files[0].OpenReadAsync();
    using StreamReader reader = new(stream);

    await ((MainViewModel)DataContext).Wallet.ImportBackup(await reader.ReadToEndAsync(), files[0].Name);
  }
}
