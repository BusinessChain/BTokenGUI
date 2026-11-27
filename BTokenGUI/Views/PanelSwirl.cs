using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace BTokenGUI.Views;

public class PanelSwirl : Panel
{
  const double DEGREES_TURN_AXIS = 30;
  const double FRACTION_RADIUS_MAX = 0.8;
  const double FRACTION_AXIS_MIN = 0.1;
  const double FRACTION_AXIS_MAX = 0.9;
  const double SPEED_ANGULAR_MIN = 0.5;
  const double SPEED_ANGULAR_MAX = 1.5;
  const double SPEED_DRIFT_MIN = 0.1;
  const double SPEED_DRIFT_MAX = 0.3;
  const double OPACITY_BACK = 0.35;
  const int MILLISECONDS_FRAME = 33;

  public static readonly StyledProperty<Point> CenterStartProperty = AvaloniaProperty.Register<PanelSwirl, Point>(nameof(CenterStart));
  public static readonly StyledProperty<double> RadiusStartProperty = AvaloniaProperty.Register<PanelSwirl, double>(nameof(RadiusStart));
  public static readonly StyledProperty<double> RadiusEndProperty = AvaloniaProperty.Register<PanelSwirl, double>(nameof(RadiusEnd));
  public static readonly StyledProperty<double> LengthAxisProperty = AvaloniaProperty.Register<PanelSwirl, double>(nameof(LengthAxis));
  public static readonly StyledProperty<double> ShiftProperty = AvaloniaProperty.Register<PanelSwirl, double>(nameof(Shift));
  public static readonly StyledProperty<int> SeedProperty = AvaloniaProperty.Register<PanelSwirl, int>(nameof(Seed));

  static readonly DateTime TimeStart = DateTime.UtcNow;

  DispatcherTimer TimerFrame = new() { Interval = TimeSpan.FromMilliseconds(MILLISECONDS_FRAME) };


  public PanelSwirl()
  {
    TimerFrame.Tick += (_, _) => InvalidateArrange();
  }

  public Point CenterStart
  {
    get { return GetValue(CenterStartProperty); }
    set { SetValue(CenterStartProperty, value); }
  }

  public double RadiusStart
  {
    get { return GetValue(RadiusStartProperty); }
    set { SetValue(RadiusStartProperty, value); }
  }

  public double RadiusEnd
  {
    get { return GetValue(RadiusEndProperty); }
    set { SetValue(RadiusEndProperty, value); }
  }

  public double LengthAxis
  {
    get { return GetValue(LengthAxisProperty); }
    set { SetValue(LengthAxisProperty, value); }
  }

  public double Shift
  {
    get { return GetValue(ShiftProperty); }
    set { SetValue(ShiftProperty, value); }
  }

  public int Seed
  {
    get { return GetValue(SeedProperty); }
    set { SetValue(SeedProperty, value); }
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);

    TimerFrame.Start();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnDetachedFromVisualTree(e);

    TimerFrame.Stop();
  }

  protected override Size MeasureOverride(Size availableSize)
  {
    foreach (Control child in Children)
      child.Measure(Size.Infinity);

    return new Size();
  }

  protected override Size ArrangeOverride(Size finalSize)
  {
    double seconds = (DateTime.UtcNow - TimeStart).TotalSeconds;
    double cosTurn = Math.Cos(DEGREES_TURN_AXIS * Math.PI / 180);
    double sinTurn = Math.Sin(DEGREES_TURN_AXIS * Math.PI / 180);

    foreach (Control child in Children)
    {
      Random random = new(child.DataContext.GetHashCode() + Seed);

      double phaseOrbit = 2 * Math.PI * random.NextDouble();
      double speedAngular = SPEED_ANGULAR_MIN + (SPEED_ANGULAR_MAX - SPEED_ANGULAR_MIN) * random.NextDouble();
      double fractionRadius = FRACTION_RADIUS_MAX * random.NextDouble();
      double phaseDrift = 2 * Math.PI * random.NextDouble();
      double speedDrift = SPEED_DRIFT_MIN + (SPEED_DRIFT_MAX - SPEED_DRIFT_MIN) * random.NextDouble();

      double angle = phaseOrbit + speedAngular * seconds;
      double cosOrbit = Math.Cos(angle);
      double sinOrbit = Math.Sin(angle);
      double fractionAxis = Shift + (FRACTION_AXIS_MIN + FRACTION_AXIS_MAX) / 2 + (FRACTION_AXIS_MAX - FRACTION_AXIS_MIN) / 2 * Math.Sin(phaseDrift + speedDrift * seconds);
      double along = LengthAxis * fractionAxis;
      double radius = fractionRadius * (RadiusStart + (RadiusEnd - RadiusStart) * Math.Clamp(fractionAxis, 0, 1));

      double x = along * cosTurn - radius * sinOrbit * sinTurn;
      double y = radius * cosOrbit;
      double z = along * sinTurn + radius * sinOrbit * cosTurn;
      double depthOrbit = (-3 * sinOrbit * sinTurn - 2 * cosOrbit - 6 * sinOrbit * cosTurn) / 7;

      Size size = child.DesiredSize;
      Point center = new(CenterStart.X + 0.8944 * x + 0.4472 * z, CenterStart.Y + 0.1278 * x + 0.9583 * y - 0.2556 * z);

      child.Opacity = OPACITY_BACK + (1 - OPACITY_BACK) * (1 + depthOrbit) / 2;
      child.ZIndex = (int)Math.Round(3 * x - 2 * y - 6 * z);
      child.Arrange(new Rect(new Point(center.X - size.Width / 2, center.Y - size.Height / 2), size));
    }

    return finalSize;
  }
}
