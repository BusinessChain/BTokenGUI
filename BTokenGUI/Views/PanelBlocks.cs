using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using BTokenGUI.Models;

namespace BTokenGUI.Views;

public class PanelBlocks : Panel
{
  const double SPACING_BLOCKS = 36;
  const double SECONDS_COMPRESSION = 600;
  const double FRICTION_RAIL = 1;
  const int MILLISECONDS_COMPRESSION = 250;

  public static readonly AttachedProperty<double> OffsetProperty = AvaloniaProperty.RegisterAttached<PanelBlocks, Control, double>("Offset");

  DispatcherTimer TimerCompression = new() { Interval = TimeSpan.FromMilliseconds(MILLISECONDS_COMPRESSION) };


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
    double width = 0;
    double height = 0;

    foreach (Control child in Children)
    {
      child.Measure(Size.Infinity);

      width += child.DesiredSize.Width;
      height = Math.Max(height, child.DesiredSize.Height);
    }

    if (Children.Count > 0)
      width += (Children.Count - 1) * SPACING_BLOCKS;

    return new Size(width, height);
  }

  protected override Size ArrangeOverride(Size finalSize)
  {
    foreach (Control child in Children)
      child.Arrange(new Rect(GetPosition(child) + child.GetValue(OffsetProperty), 0, child.DesiredSize.Width, finalSize.Height));

    return finalSize;
  }

  public double GetPosition(Control child)
  {
    double[] timestamps = Children.Select(control => (double)((Blockchain.Block)control.DataContext).UnixTimeSeconds).ToArray();
    double timeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    double position = Children.SkipLast(1).Sum(control => control.DesiredSize.Width + SPACING_BLOCKS);

    for (int gap = 0; gap < Children.Count; gap++)
      position -= GetWidthGap(timestamps, gap, timestamps[^1]) - GetWidthGap(timestamps, gap, timeNow);

    for (int i = Children.Count - 1; i > Children.IndexOf(child); i--)
      position -= Children[i - 1].DesiredSize.Width + GetWidthGap(timestamps, i, timeNow);

    return position;
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
}
