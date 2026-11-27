using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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
  const double SPEED_OUTFLOW = 128;
  const double SECONDS_GATHER = 1;
  const double SECONDS_HEAT = 1.25;
  const double SECONDS_INFUSE = 1;
  const double SECONDS_COOL = 1.25;
  const double SECONDS_PART = 1;
  const double OPACITY_PLASMA = 0.85;

  bool IsCyclingNeutrons;


  public MiningView()
  {
    InitializeComponent();

    ItemsFlights.ContainerPrepared += StartFlight;
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
    Control controlOrigin = isOutflow ? PanelConverter : FindControl(good.Origin) ?? ItemsBlocksBitcoin;

    if (FindCenter(controlOrigin, ItemsFlights) is Point pointOrigin)
    {
      List<Point> route = [pointOrigin];

      Canvas.SetLeft(container, pointOrigin.X - OFFSET_CENTER_GEM);
      Canvas.SetTop(container, pointOrigin.Y - OFFSET_CENTER_GEM);
      container.Opacity = isInflow || isOutflow ? 0 : 1;

      if (isInflow)
        _ = CycleNeutrons();

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
          isInflow ? SECONDS_INFLOW : isPiped ? (route.Count - 1) * SPACING_ROUTE / SPEED_OUTFLOW : SECONDS_FLIGHT,
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

  async Task CycleNeutrons()
  {
    if (IsCyclingNeutrons)
      return;

    IsCyclingNeutrons = true;

    Panel panelTank = ItemsTXsPool.ItemsPanelRoot;
    Panel panelENG = ItemsNeutronsENG.ItemsPanelRoot;
    Panel panelInfused = ItemsNeutronsInfused.ItemsPanelRoot;

    await Task.WhenAll(
      Animate(panelTank, 0, SECONDS_GATHER, new CubicEaseInOut(), (PanelSwirl.ShiftProperty, [0, 1])),
      Animate(panelENG, 0, SECONDS_GATHER, new CubicEaseInOut(), (PanelSwirl.ShiftProperty, [-1, 0])),
      Animate(panelInfused, 0, SECONDS_GATHER, new LinearEasing(), (Visual.OpacityProperty, [0, 0]), (PanelSwirl.ShiftProperty, [0, 0])));

    await Animate(PathPlasma, 0, SECONDS_HEAT, new LinearEasing(), (Visual.OpacityProperty, [0, OPACITY_PLASMA]));
    await Animate(panelInfused, 0, SECONDS_INFUSE, new LinearEasing(), (Visual.OpacityProperty, [0, 1]));
    await Animate(PathPlasma, 0, SECONDS_COOL, new LinearEasing(), (Visual.OpacityProperty, [OPACITY_PLASMA, 0]));

    await Task.WhenAll(
      Animate(panelTank, 0, SECONDS_PART, new CubicEaseInOut(), (PanelSwirl.ShiftProperty, [1, 0])),
      Animate(panelENG, 0, SECONDS_PART, new CubicEaseInOut(), (PanelSwirl.ShiftProperty, [0, -1])),
      Animate(panelInfused, 0, SECONDS_PART, new CubicEaseInOut(), (PanelSwirl.ShiftProperty, [0, 1])));

    IsCyclingNeutrons = false;
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
      return TraceRoutePath("pipeAnchor", blockchain, target);

    if (isInflow)
      return TraceRoutePath("tubeConverter", blockchain, target);

    if (good is Blockchain.Anchor && good.Origin == blockchain.Anchors)
      return TraceRoutePath("busbarAnchor", blockchain, target);

    if (good is Blockchain.Chip && good.Origin is Blockchain.Block)
      return TraceRoutePath("busbarChip", blockchain, target).AsEnumerable().Reverse().ToList();

    return [];
  }

  static Point? FindCenter(Control control, Visual target)
  {
    return control?.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), target);
  }

  Path FindPath(string classPath, Blockchain blockchain)
  {
    return this.GetVisualDescendants().OfType<Path>().FirstOrDefault(path => path.Classes.Contains(classPath) && path.DataContext == blockchain);
  }

  List<Point> TraceRoutePath(string classPath, Blockchain blockchain, Visual target)
  {
    return FindPath(classPath, blockchain) is Path path ? SampleRoute(path.Data).Select(point => path.TranslatePoint(point, target)).OfType<Point>().ToList() : [];
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
}
