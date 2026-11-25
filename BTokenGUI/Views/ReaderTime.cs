using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace BTokenGUI.Views;

public class ReaderTime : Control
{
  const double HEIGHT_RAIL = 10;
  const double HEIGHT_TICK_MINUTE = 6;
  const double HEIGHT_TICK_TEN_MINUTES = 10;
  const double HEIGHT_STOPPER = 22;
  const double WIDTH_STOPPER = 7;
  const double OFFSET_STOPPER = 20;

  static readonly ImmutablePen PenTick = new(Color.Parse("#F2F4F7").ToUInt32(), 1);
  static readonly ImmutablePen PenStopper = new(Color.Parse("#3A4048").ToUInt32(), 1);
  static readonly ImmutableLinearGradientBrush BrushStopper = new(
    [
      new ImmutableGradientStop(0, Color.Parse("#6B737D")),
      new ImmutableGradientStop(0.35, Color.Parse("#E4E8EC")),
      new ImmutableGradientStop(0.6, Color.Parse("#A9B0B8")),
      new ImmutableGradientStop(1, Color.Parse("#5A616A"))
    ],
    startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
    endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));

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

    double positionStopper = PanelBlocks.FindAncestorOfType<ItemsControl>().TranslatePoint(new Point(), this).Value.X + OFFSET_STOPPER;

    context.DrawRectangle(BrushStopper, PenStopper, new Rect(positionStopper - WIDTH_STOPPER, center - HEIGHT_STOPPER / 2, WIDTH_STOPPER, HEIGHT_STOPPER), 1.5, 1.5);
  }
}
