using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BTokenGUI.Models;

namespace BTokenGUI.Views;

public partial class ChainView : UserControl
{
  const double OFFSET_CENTER_GEM = 16;
  const double SECONDS_FLIGHT = 0.8;
  const double SECONDS_SLIDE = 0.5;


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
      Animation animation = CreateAnimation(
        SECONDS_FLIGHT,
        (Canvas.LeftProperty, pointStart.X - OFFSET_CENTER_GEM, pointEnd.X - OFFSET_CENTER_GEM),
        (Canvas.TopProperty, pointStart.Y - OFFSET_CENTER_GEM, pointEnd.Y - OFFSET_CENTER_GEM));

      container.Opacity = 1;

      await animation.RunAsync(container);
    }

    ((Blockchain)itemsFlights.DataContext).Land(good);
  }

  async void SlideBlocks(object sender, ContainerPreparedEventArgs e)
  {
    ItemsControl itemsBlocks = (ItemsControl)sender;
    Blockchain blockchain = (Blockchain)itemsBlocks.DataContext;

    if (!blockchain.IsTipExtending)
      return;

    blockchain.IsPanelLocked = true;

    Control[] containersSliding = itemsBlocks.GetRealizedContainers().Where(container => container != e.Container).ToArray();
    double offset = containersSliding[0].Bounds.Width + ((StackPanel)itemsBlocks.ItemsPanelRoot).Spacing;

    e.Container.Opacity = 0;

    foreach (Control container in containersSliding)
      container.RenderTransform = new TranslateTransform(offset, 0);

    await Task.WhenAll(containersSliding
      .Select(container => CreateAnimation(SECONDS_SLIDE, (TranslateTransform.XProperty, offset, 0)).RunAsync(container))
      .Append(CreateAnimation(SECONDS_SLIDE, (Visual.OpacityProperty, 0, 1)).RunAsync(e.Container)));

    foreach (Control container in containersSliding)
      container.RenderTransform = null;

    e.Container.Opacity = 1;
    blockchain.IsPanelLocked = false;
  }

  void LoadTXsFlyout(object sender, EventArgs e)
  {
    _ = ((Blockchain.Block)((Flyout)sender).Target.DataContext).LoadTXs();
  }

  static Animation CreateAnimation(double seconds, params (AvaloniaProperty Property, double ValueStart, double ValueEnd)[] transitions)
  {
    KeyFrame keyFrameStart = new() { Cue = new Cue(0) };
    KeyFrame keyFrameEnd = new() { Cue = new Cue(1) };

    foreach ((AvaloniaProperty property, double valueStart, double valueEnd) in transitions)
    {
      keyFrameStart.Setters.Add(new Setter(property, valueStart));
      keyFrameEnd.Setters.Add(new Setter(property, valueEnd));
    }

    return new Animation
    {
      Duration = TimeSpan.FromSeconds(seconds),
      Easing = new CubicEaseInOut(),
      FillMode = FillMode.Forward,
      Children = { keyFrameStart, keyFrameEnd }
    };
  }

  ItemsControl FindPlace(IList place)
  {
    return this.GetVisualDescendants()
      .OfType<ItemsControl>()
      .FirstOrDefault(itemsControl => itemsControl.ItemsSource == place);
  }
}
