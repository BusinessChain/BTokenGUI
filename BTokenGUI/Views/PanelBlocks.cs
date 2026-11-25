using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BTokenGUI.Models;

namespace BTokenGUI.Views;

public class PanelBlocks : Panel
{
  const double SPACING_BLOCKS = 36;
  const double SECONDS_COMPRESSION = 600;
  const double FRICTION_RAIL = 1;
  const int MILLISECONDS_COMPRESSION = 250;
  const double DISTANCE_MIN_TICKS = 2;
  const int MINUTES_READER = 120;

  public static readonly AttachedProperty<double> OffsetProperty = AvaloniaProperty.RegisterAttached<PanelBlocks, Control, double>("Offset");

  DispatcherTimer TimerCompression = new() { Interval = TimeSpan.FromMilliseconds(MILLISECONDS_COMPRESSION) };
  ReaderTime ReaderTime;


  static PanelBlocks()
  {
    AffectsParentArrange<PanelBlocks>(OffsetProperty);
  }

  public PanelBlocks()
  {
    TimerCompression.Tick += (_, _) => InvalidateArrange();
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);

    TimerCompression.Start();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnDetachedFromVisualTree(e);

    TimerCompression.Stop();
  }

  public double GetDistanceSlot()
  {
    return Children[0].Bounds.Width + SPACING_BLOCKS;
  }

  protected override Size MeasureOverride(Size availableSize)
  {
    double height = 0;

    foreach (Control child in Children)
    {
      child.Measure(Size.Infinity);

      height = Math.Max(height, child.DesiredSize.Height);
    }

    return new Size(GetWidthFull(), height);
  }

  public double GetWidthFull()
  {
    return Children.Sum(child => child.DesiredSize.Width) + Math.Max(0, Children.Count - 1) * SPACING_BLOCKS;
  }

  protected override Size ArrangeOverride(Size finalSize)
  {
    foreach (Control child in Children)
      child.Arrange(new Rect(GetPosition(child) + child.GetValue(OffsetProperty), 0, child.DesiredSize.Width, finalSize.Height));

    if (ReaderTime == null)
    {
      ReaderTime = ((Panel)this.FindAncestorOfType<ItemsControl>().Parent).Children.OfType<ReaderTime>().First();
      ReaderTime.PanelBlocks = this;
    }

    ReaderTime.InvalidateVisual();

    return finalSize;
  }

  public double GetPosition(Control child)
  {
    double[] timestamps = GetTimestamps();
    double timeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    double position = GetWidthFull() - Children[^1].DesiredSize.Width - GetDisplacementTip(timestamps, timeNow);

    for (int i = Children.Count - 1; i > Children.IndexOf(child); i--)
      position -= Children[i - 1].DesiredSize.Width + GetWidthGap(timestamps, i, timeNow);

    return position;
  }

  double[] GetTimestamps()
  {
    return Children.Select(control => (double)((Blockchain.Block)control.DataContext).UnixTimeSeconds).ToArray();
  }

  static double GetDisplacementTip(double[] timestamps, double time)
  {
    return Enumerable.Range(0, timestamps.Length).Sum(gap => GetWidthGap(timestamps, gap, timestamps[^1]) - GetWidthGap(timestamps, gap, time));
  }

  static double GetWidthGap(double[] timestamps, int gap, double time)
  {
    double pressure = 0;

    for (int epoch = gap; epoch < timestamps.Length; epoch++)
    {
      double timeEnd = epoch + 1 < timestamps.Length ? Math.Min(timestamps[epoch + 1], time) : time;
      double force = Math.Max(0, timeEnd - timestamps[epoch]) / SECONDS_COMPRESSION - FRICTION_RAIL * (epoch - gap);

      pressure = Math.Max(pressure, force);
    }

    return SPACING_BLOCKS / (1 + pressure);
  }

  public List<(double Position, int Minute)> GetTicks()
  {
    List<(double Position, int Minute)> ticks = [];

    if (Children.Count == 0)
      return ticks;

    double[] timestamps = GetTimestamps();
    double widthFull = GetWidthFull();
    double[] positionsMinute = Enumerable.Range(0, MINUTES_READER + 1)
      .Select(minute => widthFull - GetDisplacementTip(timestamps, timestamps[^1] + 60 * minute))
      .ToArray();

    int minute = 0;
    int step = 1;

    while (minute + step <= MINUTES_READER)
    {
      if (positionsMinute[minute] - positionsMinute[minute + step] >= DISTANCE_MIN_TICKS)
      {
        minute += step;
        ticks.Add((positionsMinute[minute], minute));
      }
      else if (step == 1)
      {
        step = 10;
        minute -= minute % 10;
      }
      else
        break;
    }

    return ticks;
  }
}
