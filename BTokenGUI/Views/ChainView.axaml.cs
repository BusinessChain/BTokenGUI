using System;
using System.Collections;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BTokenGUI.Models;

namespace BTokenGUI.Views;

public partial class ChainView : UserControl
{
  const double OFFSET_CENTER_GEM = 16;
  const double SECONDS_FLIGHT = 0.8;


  public ChainView()
  {
    InitializeComponent();

    ItemsFlights.ContainerPrepared += StartFlight;
    ItemsFlightsBToken.ContainerPrepared += StartFlight;
  }

  async void StartFlight(object sender, ContainerPreparedEventArgs e)
  {
    ItemsControl itemsFlights = (ItemsControl)sender;
    Control container = e.Container;
    Blockchain.Good good = (Blockchain.Good)container.DataContext;

    container.Opacity = 0;

    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

    Control controlOrigin = good.Origin == null ? PanelConverter : (Control)FindPlace(good.Origin) ?? ContentBlockchainBitcoin;
    Control controlDestination = this.GetVisualDescendants()
      .OfType<ContentPresenter>()
      .FirstOrDefault(presenter => presenter.DataContext == good && ItemsControl.ItemsControlFromItemContainer(presenter) is ItemsControl itemsControl && itemsControl != itemsFlights);

    if (controlDestination != null
      && controlOrigin.TranslatePoint(new Point(controlOrigin.Bounds.Width / 2, controlOrigin.Bounds.Height / 2), itemsFlights) is Point pointStart
      && controlDestination.TranslatePoint(new Point(controlDestination.Bounds.Width / 2, controlDestination.Bounds.Height / 2), itemsFlights) is Point pointEnd)
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

    ((Blockchain)itemsFlights.DataContext).Land(good);
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

  ItemsControl FindPlace(IList place)
  {
    return this.GetVisualDescendants()
      .OfType<ItemsControl>()
      .FirstOrDefault(itemsControl => itemsControl.ItemsSource == place);
  }
}
