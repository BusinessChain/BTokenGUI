using Avalonia;
using Avalonia.Controls;

namespace BTokenGUI.Views;

public class PanelHeap : Panel
{
  const int COUNT_ROWS_HEAP = 5;

  protected override Size MeasureOverride(Size availableSize)
  {
    foreach (Control child in Children)
      child.Measure(Size.Infinity);

    return new Size();
  }

  protected override Size ArrangeOverride(Size finalSize)
  {
    int row = 0;
    int column = 0;

    foreach (Control child in Children)
    {
      Size size = child.DesiredSize;

      child.Arrange(new Rect(new Point(finalSize.Width / 2 + (column - row / 2.0 - 0.5) * size.Width, finalSize.Height - (row + 1) * size.Height), size));

      if (++column > row)
      {
        row = (row + 1) % COUNT_ROWS_HEAP;
        column = 0;
      }
    }

    return finalSize;
  }
}
