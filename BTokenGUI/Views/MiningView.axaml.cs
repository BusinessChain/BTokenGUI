using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BTokenGUI.Models;

namespace BTokenGUI.Views;

public partial class MiningView : UserControl
{
  const double OFFSET_CENTER_GEM = 16;
  const double SECONDS_FLIGHT = 2.4;
  const double SECONDS_INFLOW = 3;
  const double SECONDS_REACTION = 3;
  const double SECONDS_DRAW = 1.25;
  const double SECONDS_CREATION = 2 * SECONDS_DRAW;
  const double SPACING_ROUTE = 6;
  const double SPEED_PIPE = 320.0 / 3;
  const double SPEED_OUTFLOW = 128;
  const double LENGTH_PLUG = 11;
  const double WIDTH_PLUG = 10;
  const double LEAD_PIPE = 6;
  const double BOW_PIPE = 12;
  const double TENSION_PIPE = 0.55;
  const double DISTANCE_HANG = 48;
  const double LENGTH_HANG = 40;

  static readonly Point ContactPortUpper = new(8, 8);
  static readonly Point ContactPortLower = new(8, 5.6);

  FlexPipe[] Pipes;


  public MiningView()
  {
    InitializeComponent();

    ItemsFlights.ContainerPrepared += StartFlight;

    Pipes =
    [
      CreatePipe(ContentBlockchainBitcoin, GridMining, "tank", "portLid", HorizontalAlignment.Left),
      CreatePipe(ContentBlockchainBitcoin, GridMining, "tankAnchor", "portNozzle", HorizontalAlignment.Right),
      CreatePipe(ContentBlockchainBToken, PanelTankBToken, "tank", "portLid", HorizontalAlignment.Left)
    ];

    LayoutUpdated += (_, _) => PlacePipes();
  }

  FlexPipe CreatePipe(ContentControl chain, Control ownerCones, string classCone, string classPort, HorizontalAlignment sideHang)
  {
    ControlTheme themePlug = (ControlTheme)this.FindResource("ThemePlug");
    Path outline = new() { Stroke = new SolidColorBrush(Color.Parse("#7D8794")), StrokeThickness = 8 };
    Path core = new() { Stroke = new SolidColorBrush(Color.Parse("#F2F4F7")), StrokeThickness = 6 };
    TemplatedControl plugTip = new() { Theme = themePlug };
    TemplatedControl plugCone = new() { Theme = themePlug };

    CanvasPipes.Children.AddRange([outline, core, plugTip]);
    CanvasFront.Children.Add(plugCone);

    return new FlexPipe(chain, ownerCones, classCone, classPort, sideHang, outline, core, plugTip, plugCone);
  }

  static ContentControl FindCone(Control ownerCones, string classCone)
  {
    return ownerCones.GetVisualDescendants().OfType<ContentControl>().FirstOrDefault(control => control.Classes.Contains(classCone));
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

  async void StartFlight(object sender, ContainerPreparedEventArgs e)
  {
    Control container = e.Container;
    Blockchain.Good good = (Blockchain.Good)container.DataContext;
    Blockchain blockchain = good.Chain;

    container.Opacity = 0;

    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

    bool isOutflow = good.Origin == null;
    Control controlPlace = FindControl(good);
    bool isInflow = controlPlace == null && good is Blockchain.Chip;
    Control controlOrigin = isOutflow ? PanelConverter : FindControl(good.Origin) ?? ContentBlockchainBitcoin;

    if (FindCenter(controlOrigin, ItemsFlights) is Point pointOrigin)
    {
      List<Point> route = [pointOrigin];

      Canvas.SetLeft(container, pointOrigin.X - OFFSET_CENTER_GEM);
      Canvas.SetTop(container, pointOrigin.Y - OFFSET_CENTER_GEM);
      container.Opacity = isInflow || isOutflow ? 0 : 1;

      if (isInflow || isOutflow)
        await Task.Delay(TimeSpan.FromSeconds(SECONDS_CREATION + (isOutflow ? SECONDS_INFLOW + SECONDS_REACTION : 0)));

      container.Opacity = 1;

      if (FindCenter(isInflow ? PanelConverter : controlPlace, ItemsFlights) is Point pointEnd)
      {
        List<Point> routeDuct = TraceRouteDuct(blockchain, good, isInflow, isOutflow, ItemsFlights);

        foreach (Point point in routeDuct)
          ExtendRoute(route, point);

        bool isPiped = isOutflow || routeDuct.Count > 0;

        if (isPiped)
          ExtendRoute(route, pointEnd);
        else
          route.Add(pointEnd);

        await AnimateRoute(
          container,
          route,
          isInflow ? SECONDS_INFLOW : isPiped ? (route.Count - 1) * SPACING_ROUTE / (isOutflow ? SPEED_OUTFLOW : SPEED_PIPE) : SECONDS_FLIGHT,
          isPiped ? new LinearEasing() : new CubicEaseInOut());

        if (isInflow && FindCenter(PanelConverter, CanvasFront) is Point pointSphere)
        {
          Canvas.SetLeft(PanelFlash, pointSphere.X - PanelFlash.Width / 2);
          Canvas.SetTop(PanelFlash, pointSphere.Y - PanelFlash.Height / 2);

          double[] scalesFlash = [0.15, 0.45, 0.55, 0.65];

          await Task.WhenAll(
            AnimateGlow(container),
            Animate(PanelFlash, 0, SECONDS_REACTION, new LinearEasing(), (Visual.OpacityProperty, [0, 0.85, 0.85, 0])),
            Animate(PanelFlash, 0, SECONDS_REACTION, new CubicEaseOut(), (ScaleTransform.ScaleXProperty, scalesFlash), (ScaleTransform.ScaleYProperty, scalesFlash)));
        }
      }
    }

    blockchain.Land(good);
  }

  static Task AnimateGlow(Control container)
  {
    Path flash = container.GetVisualDescendants().OfType<Path>().First(path => path.Classes.Contains("flash"));

    return Animate(flash, 0, SECONDS_REACTION, new LinearEasing(), (Visual.OpacityProperty, [0, 1, 1, 0]));
  }

  static Task AnimateRoute(Animatable target, List<Point> route, double seconds, Easing easing)
  {
    return Animate(
      target,
      0,
      seconds,
      easing,
      (Canvas.LeftProperty, route.Select(point => point.X - OFFSET_CENTER_GEM).ToArray()),
      (Canvas.TopProperty, route.Select(point => point.Y - OFFSET_CENTER_GEM).ToArray()));
  }

  List<Point> TraceRouteDuct(Blockchain blockchain, Blockchain.Good good, bool isInflow, bool isOutflow, Visual target)
  {
    if (isOutflow && good is Blockchain.Anchor)
      return FindPath("pipeAnchor", blockchain) is Path pipe ? TraceRoutePath(pipe, target) : [];

    if (isInflow)
      return FindPath("tubeConverter", blockchain) is Path tube ? TraceRoutePath(tube, target) : [];

    return TraceRouteFlex(blockchain, good).Select(point => CanvasPipes.TranslatePoint(point, target)).OfType<Point>().ToList();
  }

  static Point? FindCenter(Control control, Visual target)
  {
    return control?.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), target);
  }

  List<Point> TraceRouteFlex(Blockchain blockchain, Blockchain.Good good)
  {
    if (blockchain.Blocks.Count == 0)
      return [];

    Blockchain.Block tip = blockchain.Blocks[^1];
    bool isChipLeavingTip = good is Blockchain.Chip && good.Origin == tip;
    bool isAnchorEnteringTip = good is Blockchain.Anchor anchor && good.Origin == blockchain.Anchors && tip.Anchors.Contains(anchor);

    if (!isChipLeavingTip && !isAnchorEnteringTip)
      return [];

    List<Point> route = Pipes.First(pipe => pipe.Chain.Content == blockchain && pipe.ClassCone == (isChipLeavingTip ? "tank" : "tankAnchor")).TraceRoute();

    if (isAnchorEnteringTip)
      route.Reverse();

    return route;
  }

  Path FindPath(string classPath, Blockchain blockchain)
  {
    return this.GetVisualDescendants().OfType<Path>().FirstOrDefault(path => path.Classes.Contains(classPath) && path.DataContext == blockchain);
  }

  static List<Point> TraceRoutePath(Path path, Visual target)
  {
    return SampleRoute(path.Data).Select(point => path.TranslatePoint(point, target)).OfType<Point>().ToList();
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

  void OpenFlyoutBlock(object sender, EventArgs e)
  {
    _ = ((Blockchain.Block)((Flyout)sender).Target.DataContext).Open();
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

  Control FindControl(object shown)
  {
    return this.GetVisualDescendants()
      .OfType<Control>()
      .FirstOrDefault(control => control is ItemsControl itemsControl
        ? itemsControl.ItemsSource == shown
        : control.DataContext == shown && ItemsControl.ItemsControlFromItemContainer(control) is ItemsControl itemsOwner && itemsOwner != ItemsFlights);
  }

  class FlexPipe
  {
    public ContentControl Chain;
    public string ClassCone;
    public double Connection = 1;

    Control OwnerCones;
    string ClassPort;
    double DirectionHang;

    Path Outline;
    Path Core;
    TemplatedControl PlugTip;
    TemplatedControl PlugCone;
    Point Start;
    Point End;
    Geometry Branch;


    public FlexPipe(ContentControl chain, Control ownerCones, string classCone, string classPort, HorizontalAlignment sideHang, Path outline, Path core, TemplatedControl plugTip, TemplatedControl plugCone)
    {
      Chain = chain;
      OwnerCones = ownerCones;
      ClassCone = classCone;
      ClassPort = classPort;
      DirectionHang = sideHang == HorizontalAlignment.Left ? -1 : 1;
      Outline = outline;
      Core = core;
      PlugTip = plugTip;
      PlugCone = plugCone;
    }

    public void Place(Visual canvas)
    {
      Point? contactCone = FindPort(FindCone(OwnerCones, ClassCone), ClassPort)?.TranslatePoint(ContactPortLower, canvas);

      bool isPlaced = contactCone != null;

      Outline.IsVisible = isPlaced;
      Core.IsVisible = isPlaced;
      PlugTip.IsVisible = isPlaced;
      PlugCone.IsVisible = isPlaced;

      if (!isPlaced)
        return;

      Point rest = new(contactCone.Value.X + DirectionHang * DISTANCE_HANG, contactCone.Value.Y + LENGTH_HANG);
      Point contactPlug = rest;
      double connection = 0;

      if (Connection > 0 && FindPort(FindTip(Chain), "portBottom")?.TranslatePoint(ContactPortUpper, canvas) is Point contactTip)
      {
        connection = Connection * Connection * (3 - 2 * Connection);
        contactPlug = rest + (contactTip - rest) * connection;
      }

      double anglePlug = -DirectionHang * 180 * connection;
      double radiansPlug = anglePlug * Math.PI / 180;
      Vector directionLead = new(Math.Sin(radiansPlug), -Math.Cos(radiansPlug));
      Point start = contactPlug + directionLead * LENGTH_PLUG;
      Point end = new(contactCone.Value.X, contactCone.Value.Y - LENGTH_PLUG);

      if (start == Start && end == End)
        return;

      Start = start;
      End = end;

      Point centerPlug = contactPlug + directionLead * (LENGTH_PLUG / 2);

      Canvas.SetLeft(PlugTip, centerPlug.X - WIDTH_PLUG / 2);
      Canvas.SetTop(PlugTip, centerPlug.Y - LENGTH_PLUG / 2);
      PlugTip.RenderTransform = new RotateTransform(anglePlug);
      Canvas.SetLeft(PlugCone, end.X - WIDTH_PLUG / 2);
      Canvas.SetTop(PlugCone, end.Y);

      Point leadStart = start + directionLead * LEAD_PIPE;
      Point leadEnd = new(end.X, end.Y - LEAD_PIPE);
      double reach = Math.Max(LENGTH_HANG, Math.Abs(leadEnd.Y - leadStart.Y) / 2 * TENSION_PIPE);
      Point controlStart = leadStart + directionLead * reach + new Vector(DirectionHang * BOW_PIPE, 0);
      Point controlEnd = new(leadEnd.X + DirectionHang * BOW_PIPE, leadEnd.Y - reach);

      Branch = Geometry.Parse(FormattableString.Invariant($"M{start.X},{start.Y} L{leadStart.X},{leadStart.Y} C{controlStart.X},{controlStart.Y} {controlEnd.X},{controlEnd.Y} {leadEnd.X},{leadEnd.Y} L{end.X},{end.Y}"));

      Outline.Data = Branch;
      Core.Data = Branch;
    }

    public List<Point> TraceRoute()
    {
      if (!Outline.IsVisible)
        return [];

      return [new Point(Start.X, Start.Y - LENGTH_PLUG), .. SampleRoute(Branch), new Point(End.X, End.Y + LENGTH_PLUG)];
    }
  }
}
