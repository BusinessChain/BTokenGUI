using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTokenGUI.Models;

public partial class Blockchain
{
  public partial class Good : ObservableObject
  {
    public string Hash { get; protected set; }

    public IBrush BrushPair { get; protected set; }

    public object Origin { get; set; }

    [ObservableProperty]
    public partial bool IsFlying { get; set; }

    [ObservableProperty]
    public partial bool IsExploding { get; set; }


    protected static IBrush CreateBrushPair(string hash)
    {
      byte[] bytes = Convert.FromHexString(hash[^6..]);

      return new SolidColorBrush(Color.FromRgb(bytes[0], bytes[1], bytes[2]));
    }
  }
}
