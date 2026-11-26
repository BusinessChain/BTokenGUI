using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace BTokenGUI.Views;

public partial class MainWindow : Window
{
  const double FACTOR_ZOOM = 1.2;
  const double SCALE_MAX_ZOOM = 8;


  public MainWindow()
  {
    InitializeComponent();

    AddHandler(PointerWheelChangedEvent, Zoom, RoutingStrategies.Tunnel);
  }

  void Zoom(object sender, PointerWheelEventArgs e)
  {
    if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
      return;

    e.Handled = true;

    ScaleTransform scale = (ScaleTransform)LayoutZoom.LayoutTransform;
    double scaleNew = Math.Clamp(scale.ScaleX * Math.Pow(FACTOR_ZOOM, e.Delta.Y), 1, SCALE_MAX_ZOOM);
    Vector pointer = e.GetPosition(ScrollZoom);
    Vector pointContent = (ScrollZoom.Offset + pointer) / scale.ScaleX;

    scale.ScaleX = scaleNew;
    scale.ScaleY = scaleNew;
    ScrollZoom.UpdateLayout();
    ScrollZoom.Offset = pointContent * scaleNew - pointer;
  }
}
