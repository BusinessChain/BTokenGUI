using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace BTokenGUI.Views;

public class ReaderTime : Control
{
  const double HEIGHT_RAIL = 10;
  const double HEIGHT_TICK_MINUTE = 6;
  const double HEIGHT_TICK_TEN_MINUTES = 10;

  static readonly ImmutablePen PenTick = new(Color.Parse("#F2F4F7").ToUInt32(), 1);
  static readonly ImmutablePen PenBoundsPanel = new(Colors.Yellow.ToUInt32(), 1);

  public PanelBlocks PanelBlocks;


  public override void Render(DrawingContext context)
  {
    List<(double Position, int Minute)> ticks = PanelBlocks.GetTicks();

    if (ticks.Count == 0)
      return;

    double center = Bounds.Height / 2;
    double positionEnd = ticks[^1].Position;

    context.FillRectangle((IBrush)this.FindResource("SystemControlHighlightAccentBrush"), new Rect(positionEnd, center - HEIGHT_RAIL / 2, PanelBlocks.GetWidthFull() - positionEnd, HEIGHT_RAIL));

    foreach ((double position, int minute) in ticks)
    {
      double height = minute % 10 == 0 ? HEIGHT_TICK_TEN_MINUTES : HEIGHT_TICK_MINUTE;

      context.DrawLine(PenTick, new Point(position, center - height / 2), new Point(position, center + height / 2));
    }

    context.DrawRectangle(null, PenBoundsPanel, new Rect(PanelBlocks.TranslatePoint(new Point(), this).Value, PanelBlocks.Bounds.Size).Deflate(0.5));
  }
}
