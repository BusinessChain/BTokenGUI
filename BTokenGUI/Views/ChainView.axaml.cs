using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
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
  const double SPACING_ROUTE = 6;
  const double SPEED_PIPE = 320;
  const double LENGTH_PLUG = 11;
  const double WIDTH_PLUG = 10;
  const double LEAD_PIPE = 6;
  const double BOW_PIPE = 12;
  const double TENSION_PIPE = 0.55;
  const double RETRACTION_PLUG = 12;
  const double SECONDS_PLUG = 0.3;
  const int MILLISECONDS_FRAME_PLUG = 16;

  static readonly Point ContactPortUpper = new(8, 8);
  static readonly Point ContactPortLower = new(8, 5.6);

  ContentPresenter PresenterBlockLeaving;

  FlexPipe[] Pipes;


  public ChainView()
  {
    InitializeComponent();

    ItemsFlights.ContainerPrepared += StartFlight;
    ItemsFlightsBToken.ContainerPrepared += StartFlight;

    Pipes =
    [
      CreatePipe(ContentBlockchainBitcoin, isTipUpper: false),
      CreatePipe(ContentBlockchainBitcoin, isTipUpper: true),
      CreatePipe(ContentBlockchainBToken, isTipUpper: true)
    ];

    LayoutUpdated += (_, _) => PlacePipes();
  }

  FlexPipe CreatePipe(ContentControl chain, bool isTipUpper)
  {
    Path outline = new() { Stroke = new SolidColorBrush(Color.Parse("#7D8794")), StrokeThickness = 8 };
    Path core = new() { Stroke = new SolidColorBrush(Color.Parse("#F2F4F7")), StrokeThickness = 6 };
    TemplatedControl plugUpper = new() { Theme = (ControlTheme)this.FindResource("ThemePlug"), RenderTransform = new ScaleTransform(1, -1) };
    TemplatedControl plugLower = new() { Theme = (ControlTheme)this.FindResource("ThemePlug") };

    CanvasPipes.Children.AddRange([outline, core]);
    (isTipUpper ? CanvasPipes : CanvasPlugs).Children.Add(plugUpper);
    CanvasPlugs.Children.Add(plugLower);

    return new FlexPipe(chain, isTipUpper, outline, core, plugUpper, plugLower);
  }

  static ContentControl FindCone(ContentControl chain, string classCone)
  {
    return chain.GetVisualDescendants().OfType<ContentControl>().FirstOrDefault(control => control.Classes.Contains(classCone));
  }

  static Control FindTip(ContentControl chain)
  {
    ItemsControl itemsBlocks = chain.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault(control => control.Classes.Contains("blocks"));

    return itemsBlocks?.ContainerFromIndex(itemsBlocks.ItemCount - 1);
  }

  static Control FindPort(Control owner, string classPort)
  {
    return owner?.GetVisualDescendants().OfType<TemplatedControl>().FirstOrDefault(control => control.Classes.Contains(classPort) && control.IsEffectivelyVisible);
  }

  void PlacePipes()
  {
    foreach (FlexPipe pipe in Pipes)
      pipe.Place(CanvasPipes);
  }

  async Task AnimateRetraction(List<FlexPipe> pipes, double retractionStart, double retractionEnd)
  {
    DateTime timeStart = DateTime.UtcNow;
    double progress = 0;

    while (progress < 1)
    {
      progress = Math.Min(1, (DateTime.UtcNow - timeStart).TotalSeconds / SECONDS_PLUG);

      foreach (FlexPipe pipe in pipes)
      {
        pipe.Retraction = retractionStart + (retractionEnd - retractionStart) * progress * progress * (3 - 2 * progress);
        pipe.Place(CanvasPipes);
      }

      if (progress < 1)
        await Task.Delay(MILLISECONDS_FRAME_PLUG);
    }
  }
  async void StartFlight(object sender, ContainerPreparedEventArgs e)
  {
    ItemsControl itemsFlights = (ItemsControl)sender;
    Blockchain blockchain = (Blockchain)itemsFlights.DataContext;
    Control container = e.Container;
    Blockchain.Good good = (Blockchain.Good)container.DataContext;

    container.Opacity = 0;

    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

    Path pipe = good is Blockchain.Anchor && good.Origin == null
      ? this.GetVisualDescendants().OfType<Path>().FirstOrDefault(path => path.Classes.Contains("pipeAnchor") && path.DataContext == blockchain)
      : null;

    List<Point> route = [];

    if (pipe != null)
      route.AddRange(SampleRoute(pipe.Data).Select(point => pipe.TranslatePoint(point, itemsFlights)).OfType<Point>());
    else
    {
      Control controlOrigin = good.Origin == null ? PanelConverter : FindControl(good.Origin, itemsFlights) ?? ContentBlockchainBitcoin;

      if (controlOrigin.TranslatePoint(new Point(controlOrigin.Bounds.Width / 2, controlOrigin.Bounds.Height / 2), itemsFlights) is Point pointOrigin)
        route.Add(pointOrigin);
    }

    if (route.Count > 0)
    {
      Canvas.SetLeft(container, route[0].X - OFFSET_CENTER_GEM);
      Canvas.SetTop(container, route[0].Y - OFFSET_CENTER_GEM);
      container.Opacity = 1;

      await blockchain.TaskSlideBlocks;

      Control controlDestination = FindControl(good, itemsFlights) ?? (good is Blockchain.Chip ? PanelConverter : null);

      if (controlDestination?.TranslatePoint(new Point(controlDestination.Bounds.Width / 2, controlDestination.Bounds.Height / 2), itemsFlights) is Point pointEnd)
      {
        List<Point> routeFlex = FindPipeFlex(blockchain, good)?.TraceRoute() ?? [];

        foreach (Point point in routeFlex.Select(point => CanvasPipes.TranslatePoint(point, itemsFlights)).OfType<Point>())
          ExtendRoute(route, point);

        bool isPiped = pipe != null || routeFlex.Count > 0;

        if (isPiped)
          ExtendRoute(route, pointEnd);
        else
          route.Add(pointEnd);

        await Animate(
          container,
          0,
          isPiped ? (route.Count - 1) * SPACING_ROUTE / SPEED_PIPE : SECONDS_FLIGHT,
          isPiped ? new LinearEasing() : new CubicEaseInOut(),
          (Canvas.LeftProperty, route.Select(point => point.X - OFFSET_CENTER_GEM).ToArray()),
          (Canvas.TopProperty, route.Select(point => point.Y - OFFSET_CENTER_GEM).ToArray()));
      }
    }

    blockchain.Land(good);
  }

  FlexPipe FindPipeFlex(Blockchain blockchain, Blockchain.Good good)
  {
    if (blockchain.Blocks.Count == 0)
      return null;

    Blockchain.Block tip = blockchain.Blocks[^1];
    bool isChipFromTip = good is Blockchain.Chip && good.Origin == tip;
    bool isAnchorIntoTip = good is Blockchain.Anchor anchor && good.Origin == blockchain.Anchors && tip.Anchors.Contains(anchor);

    if (!isChipFromTip && !isAnchorIntoTip)
      return null;

    return Pipes.FirstOrDefault(pipe => pipe.Chain.Content == blockchain && pipe.IsTipUpper == isChipFromTip);
  }

  static void ExtendRoute(List<Point> route, Point point)
  {
    route.AddRange(SampleRoute(new LineGeometry(route[^1], point)).Skip(1));
  }

  static List<Point> SampleRoute(Geometry geometry)
  {
    double length = geometry.ContourLength;
    int countSteps = Math.Max(1, (int)Math.Ceiling(length / SPACING_ROUTE));
    List<Point> points = [];

    for (int i = 0; i <= countSteps; i++)
      if (geometry.TryGetPointAtDistance(length * i / countSteps, out Point point))
        points.Add(point);

    return points;
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
    List<FlexPipe> pipesTip = Pipes.Where(pipe => pipe.Chain.Content == itemsBlocks.DataContext).ToList();

    pipesTip.ForEach(pipe => pipe.IsTipFrozen = true);

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

    await AnimateRetraction(pipesTip, 0, 1);

    await Task.WhenAll(containersSliding
      .Select((container, index) => Animate(container, SECONDS_STAGGER_SLIDE * (index + 1), SECONDS_SLIDE, new CubicEaseInOut(), (TranslateTransform.XProperty, [offset, 0])))
      .Append(Animate(presenterLeaving, 0, SECONDS_SLIDE, new CubicEaseInOut(), (TranslateTransform.XProperty, [0, -offset])))
      .Append(Animate(presenterLeaving, 0, SECONDS_SLIDE, new LinearEasing(), (Visual.OpacityProperty, [1, 0.8, 0])))
      .Concat(isSlidingIn ? [Animate(containerNew, SECONDS_STAGGER_SLIDE * containersSliding.Length, SECONDS_SLIDE, new LinearEasing(), (Visual.OpacityProperty, [0, 1]))] : []));

    panel.Children.Remove(presenterLeaving);

    foreach (Control container in containersSliding)
      container.RenderTransform = null;

    pipesTip.ForEach(pipe => pipe.IsTipFrozen = false);

    await AnimateRetraction(pipesTip, 1, 0);
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

  class FlexPipe
  {
    public ContentControl Chain;
    public bool IsTipUpper;
    public bool IsTipFrozen;
    public double Retraction;

    Path Outline;
    Path Core;
    TemplatedControl PlugUpper;
    TemplatedControl PlugLower;
    Point? ContactTip;
    Point Start;
    Point End;


    public FlexPipe(ContentControl chain, bool isTipUpper, Path outline, Path core, TemplatedControl plugUpper, TemplatedControl plugLower)
    {
      Chain = chain;
      IsTipUpper = isTipUpper;
      Outline = outline;
      Core = core;
      PlugUpper = plugUpper;
      PlugLower = plugLower;
    }

    public void Place(Visual canvas)
    {
      Control portUpper = IsTipUpper ? FindPort(FindTip(Chain), "portBottom") : FindPort(FindCone(Chain, "tankAnchor"), "portBottom");
      Control portLower = IsTipUpper ? FindPort(FindCone(Chain, "tank"), "portTop") : FindPort(FindTip(Chain), "portTop");

      Point? contactUpper = IsTipUpper && IsTipFrozen ? ContactTip : portUpper?.TranslatePoint(ContactPortUpper, canvas);
      Point? contactLower = !IsTipUpper && IsTipFrozen ? ContactTip : portLower?.TranslatePoint(ContactPortLower, canvas);

      ContactTip = IsTipUpper ? contactUpper : contactLower;

      bool isPlaced = contactUpper != null && contactLower != null;

      Outline.IsVisible = isPlaced;
      Core.IsVisible = isPlaced;
      PlugUpper.IsVisible = isPlaced;
      PlugLower.IsVisible = isPlaced;

      if (!isPlaced)
        return;

      double retraction = Retraction * RETRACTION_PLUG;

      Point start = new(contactUpper.Value.X, contactUpper.Value.Y + LENGTH_PLUG + (IsTipUpper ? retraction : 0));
      Point end = new(contactLower.Value.X, contactLower.Value.Y - LENGTH_PLUG - (IsTipUpper ? 0 : retraction));

      Start = start;
      End = end;

      if (Canvas.GetLeft(PlugUpper) == start.X - WIDTH_PLUG / 2 && Canvas.GetTop(PlugUpper) == start.Y - LENGTH_PLUG
        && Canvas.GetLeft(PlugLower) == end.X - WIDTH_PLUG / 2 && Canvas.GetTop(PlugLower) == end.Y)
        return;

      Canvas.SetLeft(PlugUpper, start.X - WIDTH_PLUG / 2);
      Canvas.SetTop(PlugUpper, start.Y - LENGTH_PLUG);
      Canvas.SetLeft(PlugLower, end.X - WIDTH_PLUG / 2);
      Canvas.SetTop(PlugLower, end.Y);

      Point leadStart = new(start.X, start.Y + LEAD_PIPE);
      Point leadEnd = new(end.X, end.Y - LEAD_PIPE);
      Point middle = new((leadStart.X + leadEnd.X) / 2 + BOW_PIPE, (leadStart.Y + leadEnd.Y) / 2);
      double reach = (leadEnd.Y - leadStart.Y) / 2 * TENSION_PIPE;

      Geometry geometry = Geometry.Parse(FormattableString.Invariant($"""
        M{start.X},{start.Y} L{leadStart.X},{leadStart.Y}
        C{leadStart.X},{leadStart.Y + reach} {middle.X},{middle.Y - reach} {middle.X},{middle.Y}
        C{middle.X},{middle.Y + reach} {leadEnd.X},{leadEnd.Y - reach} {leadEnd.X},{leadEnd.Y} L{end.X},{end.Y}
        """));

      Outline.Data = geometry;
      Core.Data = geometry;
    }

    public List<Point> TraceRoute()
    {
      if (!Outline.IsVisible || Outline.Data == null)
        return [];

      return [new Point(Start.X, Start.Y - LENGTH_PLUG), .. SampleRoute(Outline.Data), new Point(End.X, End.Y + LENGTH_PLUG)];
    }
  }
}
