using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTokenGUI.Models;

public partial class Mining
{
  public partial class Wallet : ObservableObject
  {
    Mining Mining;

    public string MethodWithdrawal { get; }

    public string TitleWithdrawal { get; }

    public bool IsAutoOffered { get; }

    [ObservableProperty]
    public partial string Address { get; set; }

    [ObservableProperty]
    public partial long? Funds { get; set; }

    [ObservableProperty]
    public partial double Filling { get; set; }

    [ObservableProperty]
    public partial bool IsAutoActive { get; set; }

    [ObservableProperty]
    public partial bool IsAutoToWalletGUI { get; set; }


    public Wallet(Mining mining, string methodWithdrawal, string titleWithdrawal, bool isAutoOffered)
    {
      Mining = mining;
      MethodWithdrawal = methodWithdrawal;
      TitleWithdrawal = titleWithdrawal;
      IsAutoOffered = isAutoOffered;
    }

    [RelayCommand]
    void OpenPanel()
    {
      Mining.OpenFormWithdrawal(this);
    }
  }
}
