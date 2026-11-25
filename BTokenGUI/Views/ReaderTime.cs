using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace BTokenGUI.Views;

public class ReaderTime : Control
{
  static readonly ImmutablePen PenBoundsPanel = new(Colors.Yellow.ToUInt32(), 1);

  public PanelBlocks PanelBlocks;


  public override void Render(DrawingContext context)
  {
    context.DrawRectangle(null, PenBoundsPanel, new Rect(PanelBlocks.TranslatePoint(new Point(), this).Value, PanelBlocks.Bounds.Size).Deflate(0.5));
  }
}
