using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace BTokenGUI.Views;

public class PanelSwirl : Panel
{
  const double X_CENTER_END_WIDE = 17.646;
  const double Y_CENTER_END_WIDE = 34.395;
  const double RADIUS_END_WIDE = 34;
  const double RADIUS_END_NARROW = 11;
  const double LENGTH_AXIS = 40;
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

  static readonly DateTime TimeStart = DateTime.UtcNow;

  DispatcherTimer TimerFrame = new() { Interval = TimeSpan.FromMilliseconds(MILLISECONDS_FRAME) };


  public PanelSwirl()
  {
    TimerFrame.Tick += (_, _) => InvalidateArrange();
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
      Random random = new(child.DataContext.GetHashCode());

      double phaseOrbit = 2 * Math.PI * random.NextDouble();
      double speedAngular = SPEED_ANGULAR_MIN + (SPEED_ANGULAR_MAX - SPEED_ANGULAR_MIN) * random.NextDouble();
      double fractionRadius = FRACTION_RADIUS_MAX * random.NextDouble();
      double phaseDrift = 2 * Math.PI * random.NextDouble();
      double speedDrift = SPEED_DRIFT_MIN + (SPEED_DRIFT_MAX - SPEED_DRIFT_MIN) * random.NextDouble();

      double angle = phaseOrbit + speedAngular * seconds;
      double cosOrbit = Math.Cos(angle);
      double sinOrbit = Math.Sin(angle);
      double fractionAxis = (FRACTION_AXIS_MIN + FRACTION_AXIS_MAX) / 2 + (FRACTION_AXIS_MAX - FRACTION_AXIS_MIN) / 2 * Math.Sin(phaseDrift + speedDrift * seconds);
      double along = LENGTH_AXIS * fractionAxis;
      double radius = fractionRadius * (RADIUS_END_WIDE + (RADIUS_END_NARROW - RADIUS_END_WIDE) * fractionAxis);

      double x = along * cosTurn - radius * sinOrbit * sinTurn;
      double y = radius * cosOrbit;
      double z = along * sinTurn + radius * sinOrbit * cosTurn;
      double depthOrbit = (-3 * sinOrbit * sinTurn - 2 * cosOrbit - 6 * sinOrbit * cosTurn) / 7;

      Size size = child.DesiredSize;
      Point center = new(X_CENTER_END_WIDE + 0.8944 * x + 0.4472 * z, Y_CENTER_END_WIDE + 0.1278 * x + 0.9583 * y - 0.2556 * z);

      child.Opacity = OPACITY_BACK + (1 - OPACITY_BACK) * (1 + depthOrbit) / 2;
      child.ZIndex = (int)Math.Round(3 * x - 2 * y - 6 * z);
      child.Arrange(new Rect(new Point(center.X - size.Width / 2, center.Y - size.Height / 2), size));
    }

    return finalSize;
  }
}
