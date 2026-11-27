using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using BTokenGUI.Models;

namespace BTokenGUI.Views;

public class RailLights : Control
{
  const double SPACING_SEGMENTS = 10;
  const double Y_HOLES = 45.8;
  const double SCALE_HOLES = 2;
  const double WIDTH_HOLES = 1.6;
  const double OFFSET_HOLES_RIGHT = 2;
  const double SECONDS_STEP_CHASE = 0.05;
  const double SECONDS_PAUSE_CHASE = 5;
  const double SECONDS_DECAY_FLASH = 0.3;
  const double SECONDS_BLINK_CHORUS = 0.3;
  const int COUNT_BLINKS_CHORUS = 3;
  const double BRIGHTNESS_MIN_VISIBLE = 0.01;
  const int MILLISECONDS_FRAME = 33;

  static readonly IBrush BrushBitcoin = new SolidColorBrush(Color.Parse("#F7931A"));
  static readonly IDashStyle DashStyleHoles = new DashStyle([4, 13.5], 0);
  static readonly IPen PenHoles = new Pen(new SolidColorBrush(Color.Parse("#0B0D11")), WIDTH_HOLES, DashStyleHoles, PenLineCap.Round);
  static readonly DateTime TimeStart = DateTime.UtcNow;

  DispatcherTimer TimerFrame = new() { Interval = TimeSpan.FromMilliseconds(MILLISECONDS_FRAME) };
  DateTime TimeStartChorus = DateTime.MinValue;
  Blockchain Chain;
  IPen PenLight;


  public RailLights()
  {
    TimerFrame.Tick += (_, _) => InvalidateVisual();
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);

    TimerFrame.Start();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnDetachedFromVisualTree(e);

    TimerFrame.Stop();
  }

  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);

    if (Chain != null)
      Chain.Blocks.CollectionChanged -= StartChorus;

    Chain = DataContext as Blockchain;

    if (Chain != null)
      Chain.Blocks.CollectionChanged += StartChorus;
  }

  void StartChorus(object sender, NotifyCollectionChangedEventArgs e)
  {
    if (Chain.IsPlaced && e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex == Chain.Blocks.Count - 1)
      TimeStartChorus = DateTime.UtcNow;
  }

  public override void Render(DrawingContext context)
  {
    if (Bounds.Width <= OFFSET_HOLES_RIGHT)
      return;

    Blockchain.Block blockTip = Chain?.Blocks.LastOrDefault();
    IBrush brushTip = blockTip == null ? Brushes.White : blockTip.BrushPair ?? BrushBitcoin;

    if (PenLight?.Brush != brushTip)
      PenLight = new Pen(brushTip, WIDTH_HOLES, DashStyleHoles, PenLineCap.Round);

    Point startHoles = new(Bounds.Width - OFFSET_HOLES_RIGHT, Y_HOLES);
    Point endHoles = new(0, Y_HOLES);

    using DrawingContext.PushedState pushedScale = context.PushTransform(Matrix.CreateTranslation(0, -Y_HOLES) * Matrix.CreateScale(1, SCALE_HOLES) * Matrix.CreateTranslation(0, Y_HOLES));

    context.DrawLine(PenHoles, startHoles, endHoles);

    DateTime timeNow = DateTime.UtcNow;
    int countSegments = (int)Math.Ceiling(Bounds.Width / SPACING_SEGMENTS);
    double secondsChorus = (timeNow - TimeStartChorus).TotalSeconds;
    bool isChorus = secondsChorus < SECONDS_BLINK_CHORUS * COUNT_BLINKS_CHORUS;
    double phaseChase = (timeNow - TimeStart).TotalSeconds % (countSegments * SECONDS_STEP_CHASE + SECONDS_PAUSE_CHASE);

    for (int i = 0; i < countSegments; i++)
    {
      double brightness;

      if (isChorus)
        brightness = secondsChorus % SECONDS_BLINK_CHORUS < SECONDS_BLINK_CHORUS / 2 ? 1 : 0;
      else
      {
        double secondsFlash = phaseChase - i * SECONDS_STEP_CHASE;
        brightness = secondsFlash < 0 ? 0 : Math.Exp(-secondsFlash / SECONDS_DECAY_FLASH);
      }

      if (brightness < BRIGHTNESS_MIN_VISIBLE)
        continue;

      double xSegment = Bounds.Width - i * SPACING_SEGMENTS;

      using (context.PushClip(new Rect(xSegment - SPACING_SEGMENTS, Y_HOLES - WIDTH_HOLES, SPACING_SEGMENTS, 2 * WIDTH_HOLES)))
      using (context.PushOpacity(brightness))
        context.DrawLine(PenLight, startHoles, endHoles);
    }
  }
}
