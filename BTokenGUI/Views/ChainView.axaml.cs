using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
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
  const double SECONDS_FALL = 0.4;
  const double SECONDS_IMPACT = 0.5;
  const double HEIGHT_FALL = 36;

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

    Control controlOrigin = good.Origin == null ? PanelConverter : (Control)FindPlace(good.Origin) ?? ContentBlockchainBitcoin;

    if (controlOrigin.TranslatePoint(new Point(controlOrigin.Bounds.Width / 2, controlOrigin.Bounds.Height / 2), itemsFlights) is Point pointStart)
    {
      Canvas.SetLeft(container, pointStart.X - OFFSET_CENTER_GEM);
      Canvas.SetTop(container, pointStart.Y - OFFSET_CENTER_GEM);
      container.Opacity = 1;

      await blockchain.TaskSlideBlocks;

      Control controlDestination = this.GetVisualDescendants()
        .OfType<ContentPresenter>()
        .FirstOrDefault(presenter => presenter.DataContext == good && ItemsControl.ItemsControlFromItemContainer(presenter) is ItemsControl itemsControl && itemsControl != itemsFlights);

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
    bool isDropping = !blockNew.IsFlying;

    Control[] containersSliding = itemsBlocks.GetRealizedContainers()
      .Where(container => container != containerNew)
      .OrderBy(itemsBlocks.IndexFromContainer)
      .ToArray();

    double offset = containersSliding[0].Bounds.Width + ((StackPanel)itemsBlocks.ItemsPanelRoot).Spacing;

    PresenterBlockLeaving = null;

    foreach (Control container in containersSliding)
      container.RenderTransform = new TranslateTransform(offset, 0);

    if (isDropping)
    {
      containerNew.Opacity = 0;
      containerNew.RenderTransformOrigin = new RelativePoint(0.5, 1, RelativeUnit.Relative);
      containerNew.RenderTransform = new TransformGroup { Children = { new ScaleTransform(), new TranslateTransform(0, -HEIGHT_FALL) } };
    }

    await Task.WhenAll(containersSliding
      .Select((container, index) => Animate(container, SECONDS_STAGGER_SLIDE * (index + 1), SECONDS_SLIDE, new CubicEaseInOut(), (TranslateTransform.XProperty, [offset, 0])))
      .Append(Animate(presenterLeaving, 0, SECONDS_SLIDE, new CubicEaseInOut(), (TranslateTransform.XProperty, [0, -offset])))
      .Append(Animate(presenterLeaving, 0, SECONDS_SLIDE, new LinearEasing(), (Visual.OpacityProperty, [1, 0.8, 0]))));

    panel.Children.Remove(presenterLeaving);

    if (isDropping)
    {
      await Task.WhenAll(
        Animate(containerNew, 0, SECONDS_FALL, new CubicEaseIn(), (TranslateTransform.YProperty, [-HEIGHT_FALL, 0])),
        Animate(containerNew, 0, SECONDS_FALL, new QuadraticEaseIn(), (Visual.OpacityProperty, [0, 1])));

      Point position = containerNew.TranslatePoint(new Point(), panel).Value;

      Ellipse shockwave = new()
      {
        Width = 84,
        Height = 26,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(position.X - 6, position.Y + 75, 0, 0),
        Stroke = blockNew.BrushPair ?? (IBrush)this.FindResource("SystemControlHighlightAccentBrush"),
        StrokeThickness = 3,
        RenderTransform = new ScaleTransform(),
        IsHitTestVisible = false,
        Effect = new BlurEffect { Radius = 2 }
      };

      panel.Children.Insert(panel.Children.IndexOf(itemsBlocks), shockwave);

      foreach (Control container in containersSliding)
        container.RenderTransform = new TranslateTransform();

      await Task.WhenAll(containersSliding
        .Select(container => Animate(container, 0, SECONDS_IMPACT * 0.7, new LinearEasing(), (TranslateTransform.YProperty, [0, 3, -1, 0])))
        .Append(Animate(containerNew, 0, SECONDS_IMPACT, new LinearEasing(), (ScaleTransform.ScaleXProperty, [1, 1.1, 0.96, 1]), (ScaleTransform.ScaleYProperty, [1, 0.84, 1.04, 1])))
        .Append(Animate(shockwave, 0, SECONDS_IMPACT * 1.2, new CubicEaseOut(), (ScaleTransform.ScaleXProperty, [0.3, 1.5]), (ScaleTransform.ScaleYProperty, [0.3, 1.5]), (Visual.OpacityProperty, [1, 0]))));

      panel.Children.Remove(shockwave);
      containerNew.RenderTransform = null;
    }

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

  ItemsControl FindPlace(IList place)
  {
    return this.GetVisualDescendants()
      .OfType<ItemsControl>()
      .FirstOrDefault(itemsControl => itemsControl.ItemsSource == place);
  }
}
