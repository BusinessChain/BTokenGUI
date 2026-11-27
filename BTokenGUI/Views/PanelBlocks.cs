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
  const double GAP_MIN_BLOCKS = 0;
  const int MILLISECONDS_COMPRESSION = 250;

  public static readonly AttachedProperty<double> DistanceArrivalProperty = AvaloniaProperty.RegisterAttached<PanelBlocks, Control, double>("DistanceArrival", inherits: true);

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

    return new Size();
  }

  protected override Size ArrangeOverride(Size finalSize)
  {
    double[] positions = GetPositions(finalSize.Width);
    TopLevel topLevel = TopLevel.GetTopLevel(this);
    double scale = topLevel == null ? 1 : topLevel.RenderScaling * (this.TransformToVisual(topLevel)?.M11 ?? 1);

    for (int i = 0; i < Children.Count; i++)
    {
      positions[i] = Math.Round(positions[i] * scale) / scale;

      bool isOffRail = positions[i] <= 0;

      Children[i].Opacity = isOffRail ? 0 : 1;
      Children[i].IsHitTestVisible = !isOffRail;
      Children[i].SetValue(DistanceArrivalProperty, GetPositionCenter(0, finalSize.Width) - Children[i].DesiredSize.Width / 2 - positions[i]);
      Children[i].Arrange(new Rect(positions[i], 0, Children[i].DesiredSize.Width, finalSize.Height));
    }

    return finalSize;
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
