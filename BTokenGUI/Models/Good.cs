using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public partial class Good : ObservableObject
  {
    public string Hash { get; protected set; }

    public IList Origin { get; set; }

    [ObservableProperty]
    public partial bool IsFlying { get; set; }
  }
}
