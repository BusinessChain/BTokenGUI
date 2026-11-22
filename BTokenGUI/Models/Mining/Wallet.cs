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

    [ObservableProperty]
    public partial string Address { get; set; }

    [ObservableProperty]
    public partial long? Funds { get; set; }

    [ObservableProperty]
    public partial double Filling { get; set; }


    public Wallet(Mining mining, string methodWithdrawal, string titleWithdrawal)
    {
      Mining = mining;
      MethodWithdrawal = methodWithdrawal;
      TitleWithdrawal = titleWithdrawal;
    }

    [RelayCommand]
    void OpenPanel()
    {
      Mining.OpenFormWithdrawal(this);
    }
  }
}
