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
  static readonly (string ClassCone, string ClassPort)[] PortsCone = [("tank", "portLid"), ("tankAnchor", "portNozzle")];

  ContentPresenter PresenterBlockLeaving;

  FlexPipe[] Pipes;


  public ChainView()
  {
    InitializeComponent();

    ItemsFlights.ContainerPrepared += StartFlight;
    ItemsFlightsBToken.ContainerPrepared += StartFlight;

    Pipes =
    [
      CreatePipe(ContentBlockchainBitcoin),
      CreatePipe(ContentBlockchainBToken)
    ];

    LayoutUpdated += (_, _) => PlacePipes();
  }

  FlexPipe CreatePipe(ContentControl chain)
  {
    Path outline = new() { Stroke = new SolidColorBrush(Color.Parse("#7D8794")), StrokeThickness = 8 };
    Path core = new() { Stroke = new SolidColorBrush(Color.Parse("#F2F4F7")), StrokeThickness = 6 };
    TemplatedControl plugTip = new() { Theme = (ControlTheme)this.FindResource("ThemePlug"), RenderTransform = new ScaleTransform(1, -1) };
    TemplatedControl[] plugsCone = PortsCone.Select(_ => new TemplatedControl { Theme = (ControlTheme)this.FindResource("ThemePlug") }).ToArray();

    CanvasPipes.Children.AddRange([outline, core, plugTip]);
    CanvasPlugs.Children.AddRange(plugsCone);

    return new FlexPipe(chain, outline, core, plugTip, plugsCone);
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
        List<Point> routeFlex = TraceRouteFlex(blockchain, good);

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

  List<Point> TraceRouteFlex(Blockchain blockchain, Blockchain.Good good)
  {
    if (blockchain.Blocks.Count == 0)
      return [];

    Blockchain.Block tip = blockchain.Blocks[^1];
    FlexPipe pipeChain = Pipes.First(pipe => pipe.Chain.Content == blockchain);

    if (good is Blockchain.Chip && good.Origin == tip)
      return pipeChain.TraceRoute("tank");

    if (good is not Blockchain.Anchor anchor || good.Origin != blockchain.Anchors || !tip.Anchors.Contains(anchor))
      return [];

    List<Point> route = pipeChain.TraceRoute("tankAnchor");
    route.Reverse();

    return route;
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
    public bool IsTipFrozen;
    public double Retraction;

    Path Outline;
    Path Core;
    TemplatedControl PlugTip;
    TemplatedControl[] PlugsCone;
    Point? ContactTip;
    Point Start;
    Point?[] Ends = [];
    Geometry[] Branches = new Geometry[PortsCone.Length];


    public FlexPipe(ContentControl chain, Path outline, Path core, TemplatedControl plugTip, TemplatedControl[] plugsCone)
    {
      Chain = chain;
      Outline = outline;
      Core = core;
      PlugTip = plugTip;
      PlugsCone = plugsCone;
    }

    public void Place(Visual canvas)
    {
      Point? contactTip = IsTipFrozen ? ContactTip : FindPort(FindTip(Chain), "portBottom")?.TranslatePoint(ContactPortUpper, canvas);

      ContactTip = contactTip;

      Point?[] contactsCone = PortsCone
        .Select(port => FindPort(FindCone(Chain, port.ClassCone), port.ClassPort)?.TranslatePoint(ContactPortLower, canvas))
        .ToArray();

      bool isPlaced = contactTip != null && contactsCone.Any(contact => contact != null);

      Outline.IsVisible = isPlaced;
      Core.IsVisible = isPlaced;
      PlugTip.IsVisible = isPlaced;

      for (int i = 0; i < PlugsCone.Length; i++)
        PlugsCone[i].IsVisible = isPlaced && contactsCone[i] != null;

      if (!isPlaced)
        return;

      Point start = new(contactTip.Value.X, contactTip.Value.Y + LENGTH_PLUG + Retraction * RETRACTION_PLUG);

      Point?[] ends = contactsCone
        .Select(contact => contact is Point point ? new Point(point.X, point.Y - LENGTH_PLUG) : (Point?)null)
        .ToArray();

      if (start == Start && ends.SequenceEqual(Ends))
        return;

      Start = start;
      Ends = ends;

      Canvas.SetLeft(PlugTip, start.X - WIDTH_PLUG / 2);
      Canvas.SetTop(PlugTip, start.Y - LENGTH_PLUG);

      Point leadStart = new(start.X, start.Y + LEAD_PIPE);

      for (int i = 0; i < ends.Length; i++)
      {
        Branches[i] = null;

        if (ends[i] is not Point end)
          continue;

        Canvas.SetLeft(PlugsCone[i], end.X - WIDTH_PLUG / 2);
        Canvas.SetTop(PlugsCone[i], end.Y);

        Point leadEnd = new(end.X, end.Y - LEAD_PIPE);
        Point middle = new((leadStart.X + leadEnd.X) / 2 + BOW_PIPE, (leadStart.Y + leadEnd.Y) / 2);
        double reach = (leadEnd.Y - leadStart.Y) / 2 * TENSION_PIPE;
        double reachBranch = (leadEnd.Y - leadStart.Y) / 2;

        string curve = leadEnd.X == leadStart.X
          ? FormattableString.Invariant($"""
            C{leadStart.X},{leadStart.Y + reach} {middle.X},{middle.Y - reach} {middle.X},{middle.Y}
            C{middle.X},{middle.Y + reach} {leadEnd.X},{leadEnd.Y - reach} {leadEnd.X},{leadEnd.Y}
            """)
          : FormattableString.Invariant($"C{leadStart.X},{leadStart.Y + reachBranch} {leadEnd.X},{leadEnd.Y - reachBranch} {leadEnd.X},{leadEnd.Y}");

        Branches[i] = Geometry.Parse(FormattableString.Invariant($"M{start.X},{start.Y} L{leadStart.X},{leadStart.Y} {curve} L{end.X},{end.Y}"));
      }

      GeometryGroup geometry = new() { Children = [.. Branches.Where(branch => branch != null)] };

      Outline.Data = geometry;
      Core.Data = geometry;
    }

    public List<Point> TraceRoute(string classCone)
    {
      int index = Array.FindIndex(PortsCone, port => port.ClassCone == classCone);

      if (!Outline.IsVisible || Ends.ElementAtOrDefault(index) is not Point end)
        return [];

      return [new Point(Start.X, Start.Y - LENGTH_PLUG), .. SampleRoute(Branches[index]), new Point(end.X, end.Y + LENGTH_PLUG)];
    }
  }
}
