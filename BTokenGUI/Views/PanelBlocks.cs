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
  const double WIDTH_FACE_FRONT = 36;
  const double GAP_MIN_BLOCKS = 5;
  const double FACTOR_X_RAIL = 0.8944;
  const double FACTOR_Y_RAIL = 0.1278;
  const int MILLISECONDS_COMPRESSION = 250;

  DispatcherTimer TimerCompression = new() { Interval = TimeSpan.FromMilliseconds(MILLISECONDS_COMPRESSION) };


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

  protected override Size MeasureOverride(Size availableSize)
  {
    foreach (Control child in Children)
      child.Measure(Size.Infinity);

    return new Size(0, availableSize.Width / FACTOR_X_RAIL * FACTOR_Y_RAIL);
  }

  protected override Size ArrangeOverride(Size finalSize)
  {
    double[] positions = GetPositions(finalSize.Width / FACTOR_X_RAIL);

    for (int i = 0; i < Children.Count; i++)
      Children[i].Arrange(new Rect(new Point(FACTOR_X_RAIL * positions[i], FACTOR_Y_RAIL * positions[i]), Children[i].DesiredSize));

    return finalSize;
  }

  double[] GetPositions(double lengthRail)
  {
    double[] timestamps = GetTimestamps();
    double timeNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    double[] positions = new double[Children.Count];
    double limitRight = double.PositiveInfinity;

    for (int i = Children.Count - 1; i >= 0; i--)
    {
      positions[i] = Math.Min(GetPositionCenter(Math.Max(0, timeNow - timestamps[i]), lengthRail) - Children[i].DesiredSize.Width / FACTOR_X_RAIL / 2, limitRight - WIDTH_FACE_FRONT - GAP_MIN_BLOCKS);
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

  double GetPositionCenter(double age, double lengthRail)
  {
    double widthHalf = Children[^1].DesiredSize.Width / FACTOR_X_RAIL / 2;
    double distanceStart = lengthRail - widthHalf;

    return distanceStart - (distanceStart - widthHalf) * age / MainViewModel.SECONDS_AGE_MAX_BLOCKS;
  }

  double[] GetTimestamps()
  {
    return Children.Select(control => (double)((Blockchain.Block)control.DataContext).UnixTimeSeconds).ToArray();
  }
}
