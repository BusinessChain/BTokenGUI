using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BTokenGUI.Models;
using BTokenGUI.ViewModels;

namespace BTokenGUI.Views;

public partial class ChainView : UserControl
{
  const double OFFSET_CENTER_GEM = 16;
  const double SECONDS_FLIGHT = 0.8;


  public ChainView()
  {
    InitializeComponent();

    ItemsFlights.ContainerPrepared += StartFlight;
  }

  async void StartFlight(object sender, ContainerPreparedEventArgs e)
  {
    Control container = e.Container;
    Blockchain.Anchor anchor = (Blockchain.Anchor)container.DataContext;

    container.Opacity = 0;

    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

    Control controlOrigin = anchor.Origin == null ? PanelConverter : (Control)FindPlace(anchor.Origin) ?? ContentBlockchainBitcoin;
    ItemsControl controlDestination = FindPlace(anchor.Destination);

    if (controlDestination != null
      && controlOrigin.TranslatePoint(new Point(controlOrigin.Bounds.Width / 2, controlOrigin.Bounds.Height / 2), ItemsFlights) is Point pointStart
      && controlDestination.TranslatePoint(new Point(controlDestination.Bounds.Width, controlDestination.Bounds.Height / 2), ItemsFlights) is Point pointEnd)
    {
      Animation animation = new()
      {
        Duration = TimeSpan.FromSeconds(SECONDS_FLIGHT),
        Easing = new CubicEaseInOut(),
        FillMode = FillMode.Forward,
        Children = { CreateKeyFrame(0, pointStart), CreateKeyFrame(1, pointEnd) }
      };

      container.Opacity = 1;

      await animation.RunAsync(container);
    }

    ((MainViewModel)DataContext).BlockchainBitcoin.Land(anchor);
  }

  static KeyFrame CreateKeyFrame(double cue, Point point)
  {
    return new KeyFrame
    {
      Cue = new Cue(cue),
      Setters =
      {
        new Setter(Canvas.LeftProperty, point.X - OFFSET_CENTER_GEM),
        new Setter(Canvas.TopProperty, point.Y - OFFSET_CENTER_GEM)
      }
    };
  }

  ItemsControl FindPlace(ObservableCollection<Blockchain.Anchor> place)
  {
    return this.GetVisualDescendants()
      .OfType<ItemsControl>()
      .FirstOrDefault(itemsControl => itemsControl != ItemsFlights && itemsControl.ItemsSource == place);
  }
}
