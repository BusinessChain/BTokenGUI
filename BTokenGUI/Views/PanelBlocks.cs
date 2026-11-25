using System;
using Avalonia;
using Avalonia.Controls;

namespace BTokenGUI.Views;

public class PanelBlocks : Panel
{
  const double SPACING_BLOCKS = 36;

  public static readonly AttachedProperty<double> OffsetProperty = AvaloniaProperty.RegisterAttached<PanelBlocks, Control, double>("Offset");

  static PanelBlocks()
  {
    AffectsParentArrange<PanelBlocks>(OffsetProperty);
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
    double x = 0;

    foreach (Control child in Children)
    {
      child.Arrange(new Rect(x + child.GetValue(OffsetProperty), 0, child.DesiredSize.Width, finalSize.Height));

      x += child.DesiredSize.Width + SPACING_BLOCKS;
    }

    return finalSize;
  }
}
