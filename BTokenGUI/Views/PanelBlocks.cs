using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using BTokenGUI.Models;
using BTokenGUI.ViewModels;

namespace BTokenGUI.Views;

public class PanelBlocks : Panel
{
  const double SPACING_BLOCKS = 36;
  const double WIDTH_FACE_FRONT = 36;
  const double GAP_MIN_BLOCKS = 5;
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
    double height = 0;

    foreach (Control child in Children)
    {
      child.Measure(Size.Infinity);

      height = Math.Max(height, child.DesiredSize.Height);
    }

    return new Size(0, height);
  }

  protected override Size ArrangeOverride(Size finalSize)
  {
    double[] positions = GetPositions(finalSize.Width);

    for (int i = 0; i < Children.Count; i++)
      Children[i].Arrange(new Rect(positions[i] + Children[i].GetValue(OffsetProperty), 0, Children[i].DesiredSize.Width, finalSize.Height));

    return finalSize;
  }

  public double GetPosition(Control child)
  {
    return GetPositions(Bounds.Width)[Children.IndexOf(child)];
  }

  double[] GetPositions(double widthRail)
  {
    double[] timestamps = GetTimestamps();
    double timeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    double[] positions = new double[Children.Count];
    double limitRight = double.PositiveInfinity;

    for (int i = Children.Count - 1; i >= 0; i--)
    {
      positions[i] = Math.Min(GetPositionCenter(Math.Max(0, timeNow - timestamps[i]), widthRail) - Children[i].DesiredSize.Width / 2, limitRight - WIDTH_FACE_FRONT - GAP_MIN_BLOCKS);
      limitRight = positions[i];
    }

    double limitLeft = 0;

    for (int i = 0; i < Children.Count; i++)
    {
      positions[i] = Math.Max(positions[i], limitLeft);
      limitLeft = positions[i] + WIDTH_FACE_FRONT + GAP_MIN_BLOCKS;
    }

    return positions;
  }

  double GetPositionCenter(double age, double widthRail)
  {
    double widthHalf = Children[^1].DesiredSize.Width / 2;
    double distanceStart = widthRail - widthHalf;

    return distanceStart - (distanceStart - widthHalf) * age / MainViewModel.SECONDS_AGE_MAX_BLOCKS;
  }

  double[] GetTimestamps()
  {
    return Children.Select(control => (double)((Blockchain.Block)control.DataContext).UnixTimeSeconds).ToArray();
  }
}
