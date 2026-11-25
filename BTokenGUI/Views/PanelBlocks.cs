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
  const double OFFSET_STOPPER = 20;
  const double WIDTH_FACE_FRONT = 36;
  const double GAP_MIN_BLOCKS = 5;
  const double SECONDS_AGE_TIP_PACKED = 3000;
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
    return Children.Sum(child => child.DesiredSize.Width) + Math.Max(0, Children.Count - 1) * SPACING_BLOCKS + Children.Select(child => child.DesiredSize.Width).LastOrDefault();
  }

  protected override Size ArrangeOverride(Size finalSize)
  {
    double[] positions = GetPositions();

    for (int i = 0; i < Children.Count; i++)
      Children[i].Arrange(new Rect(positions[i] + Children[i].GetValue(OffsetProperty), 0, Children[i].DesiredSize.Width, finalSize.Height));

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
    return GetPositions()[Children.IndexOf(child)];
  }

  double[] GetPositions()
  {
    double[] timestamps = GetTimestamps();
    double timeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    double[] positions = new double[Children.Count];
    double limitRight = double.PositiveInfinity;

    for (int i = Children.Count - 1; i >= 0; i--)
    {
      positions[i] = Math.Min(GetPositionCenter(Math.Max(0, timeNow - timestamps[i])) - Children[i].DesiredSize.Width / 2, limitRight - WIDTH_FACE_FRONT - GAP_MIN_BLOCKS);
      limitRight = positions[i];
    }

    double limitLeft = GetPositionStopper();

    for (int i = 0; i < Children.Count; i++)
    {
      positions[i] = Math.Max(positions[i], limitLeft);
      limitLeft = positions[i] + WIDTH_FACE_FRONT + GAP_MIN_BLOCKS;
    }

    return positions;
  }

  double GetPositionStopper()
  {
    return this.FindAncestorOfType<ScrollViewer>().TranslatePoint(new Point(), this).Value.X + OFFSET_STOPPER;
  }

  double GetPositionCenter(double age)
  {
    double positionStopper = GetPositionStopper();
    double widthHalf = Children[^1].DesiredSize.Width / 2;
    double distanceStart = GetWidthFull() - widthHalf - positionStopper;
    double distancePacked = widthHalf + (Children.Count - 1) * (WIDTH_FACE_FRONT + GAP_MIN_BLOCKS);
    double v0 = Math.Log(distanceStart / distancePacked) / SECONDS_AGE_TIP_PACKED;

    return positionStopper + distanceStart * Math.Exp(-v0 * age);
  }

  double[] GetTimestamps()
  {
    return Children.Select(control => (double)((Blockchain.Block)control.DataContext).UnixTimeSeconds).ToArray();
  }

  public List<(double Position, int Minute)> GetTicks()
  {
    List<(double Position, int Minute)> ticks = [];

    if (Children.Count == 0)
      return ticks;

    double[] positionsMinute = Enumerable.Range(0, MINUTES_READER + 1)
      .Select(minute => GetPositionCenter(60 * minute) + Children[^1].DesiredSize.Width / 2)
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
