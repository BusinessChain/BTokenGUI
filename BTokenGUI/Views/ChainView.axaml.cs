using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
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
  const double SECONDS_STAGGER_SLIDE = 0.25;

  ContentPresenter PresenterBlockLeaving;


  public ChainView()
  {
    InitializeComponent();

    ItemsFlights.ContainerPrepared += StartFlight;
    ItemsFlightsBToken.ContainerPrepared += StartFlight;
  }

  async void StartFlight(object sender, ContainerPreparedEventArgs e)
  {
    ItemsControl itemsFlights = (ItemsControl)sender;
    Blockchain blockchain = (Blockchain)itemsFlights.DataContext;
    Control container = e.Container;
    Blockchain.Good good = (Blockchain.Good)container.DataContext;

    container.Opacity = 0;

    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

    Control controlOrigin = good.Origin == null ? PanelConverter : FindControl(good.Origin, itemsFlights) ?? ContentBlockchainBitcoin;

    if (controlOrigin.TranslatePoint(new Point(controlOrigin.Bounds.Width / 2, controlOrigin.Bounds.Height / 2), itemsFlights) is Point pointStart)
    {
      Canvas.SetLeft(container, pointStart.X - OFFSET_CENTER_GEM);
      Canvas.SetTop(container, pointStart.Y - OFFSET_CENTER_GEM);
      container.Opacity = 1;

      await blockchain.TaskSlideBlocks;

      Control controlDestination = FindControl(good, itemsFlights) ?? (good is Blockchain.Chip ? PanelConverter : null);

      if (controlDestination?.TranslatePoint(new Point(controlDestination.Bounds.Width / 2, controlDestination.Bounds.Height / 2), itemsFlights) is Point pointEnd)
        await Animate(
          container,
          0,
          SECONDS_FLIGHT,
          new CubicEaseInOut(),
          (Canvas.LeftProperty, [pointStart.X - OFFSET_CENTER_GEM, pointEnd.X - OFFSET_CENTER_GEM]),
          (Canvas.TopProperty, [pointStart.Y - OFFSET_CENTER_GEM, pointEnd.Y - OFFSET_CENTER_GEM]));
    }

    blockchain.Land(good);
  }

  void PresentBlockLeaving(object sender, ContainerClearingEventArgs e)
  {
    ItemsControl itemsBlocks = (ItemsControl)sender;

    if (!((Blockchain)itemsBlocks.DataContext).IsTipExtending)
      return;

    Panel panel = (Panel)itemsBlocks.Parent;
    Point position = e.Container.TranslatePoint(new Point(), panel).Value;

    PresenterBlockLeaving = new ContentPresenter
    {
      Content = e.Container.DataContext,
      ContentTemplate = itemsBlocks.ItemTemplate,
      HorizontalAlignment = HorizontalAlignment.Left,
      VerticalAlignment = VerticalAlignment.Top,
      Margin = new Thickness(position.X, position.Y, 0, 0),
      RenderTransform = new TranslateTransform(),
      IsHitTestVisible = false
    };

    panel.Children.Add(PresenterBlockLeaving);
  }

  void ExtendTip(object sender, ContainerPreparedEventArgs e)
  {
    ItemsControl itemsBlocks = (ItemsControl)sender;
    Blockchain blockchain = (Blockchain)itemsBlocks.DataContext;

    if (blockchain.IsTipExtending)
      blockchain.TaskSlideBlocks = SlideBlocks(itemsBlocks, e.Container);
  }

  async Task SlideBlocks(ItemsControl itemsBlocks, Control containerNew)
  {
    Panel panel = (Panel)itemsBlocks.Parent;
    ContentPresenter presenterLeaving = PresenterBlockLeaving;
    Blockchain.Block blockNew = (Blockchain.Block)containerNew.DataContext;
    bool isSlidingIn = !blockNew.IsFlying;

    Control[] containersSliding = itemsBlocks.GetRealizedContainers()
      .Where(container => isSlidingIn || container != containerNew)
      .OrderBy(itemsBlocks.IndexFromContainer)
      .ToArray();

    double offset = containersSliding[0].Bounds.Width + ((StackPanel)itemsBlocks.ItemsPanelRoot).Spacing;

    PresenterBlockLeaving = null;

    foreach (Control container in containersSliding)
      container.RenderTransform = new TranslateTransform(offset, 0);

    if (isSlidingIn)
      containerNew.Opacity = 0;

    await Task.WhenAll(containersSliding
      .Select((container, index) => Animate(container, SECONDS_STAGGER_SLIDE * (index + 1), SECONDS_SLIDE, new CubicEaseInOut(), (TranslateTransform.XProperty, [offset, 0])))
      .Append(Animate(presenterLeaving, 0, SECONDS_SLIDE, new CubicEaseInOut(), (TranslateTransform.XProperty, [0, -offset])))
      .Append(Animate(presenterLeaving, 0, SECONDS_SLIDE, new LinearEasing(), (Visual.OpacityProperty, [1, 0.8, 0])))
      .Concat(isSlidingIn ? [Animate(containerNew, SECONDS_STAGGER_SLIDE * containersSliding.Length, SECONDS_SLIDE, new LinearEasing(), (Visual.OpacityProperty, [0, 1]))] : []));

    panel.Children.Remove(presenterLeaving);

    foreach (Control container in containersSliding)
      container.RenderTransform = null;
  }

  void LoadTXsFlyout(object sender, EventArgs e)
  {
    _ = ((Blockchain.Block)((Flyout)sender).Target.DataContext).LoadTXs();
  }

  static Task Animate(Animatable target, double secondsDelay, double seconds, Easing easing, params (AvaloniaProperty Property, double[] Values)[] tracks)
  {
    Animation animation = new()
    {
      Delay = TimeSpan.FromSeconds(secondsDelay),
      Duration = TimeSpan.FromSeconds(seconds),
      Easing = easing,
      FillMode = FillMode.Forward
    };

    int countKeyFrames = tracks[0].Values.Length;

    for (int i = 0; i < countKeyFrames; i++)
    {
      KeyFrame keyFrame = new() { Cue = new Cue((double)i / (countKeyFrames - 1)) };

      foreach ((AvaloniaProperty property, double[] values) in tracks)
        keyFrame.Setters.Add(new Setter(property, values[i]));

      animation.Children.Add(keyFrame);
    }

    return animation.RunAsync(target);
  }

  Control FindControl(object shown, ItemsControl itemsFlights)
  {
    return this.GetVisualDescendants()
      .OfType<Control>()
      .FirstOrDefault(control => control is ItemsControl itemsControl
        ? itemsControl.ItemsSource == shown
        : control.DataContext == shown && ItemsControl.ItemsControlFromItemContainer(control) is ItemsControl itemsOwner && itemsOwner != itemsFlights);
  }
}
