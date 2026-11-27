using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace BTokenGUI.Views;

public abstract class Block : TemplatedControl
{
  public static readonly StyledProperty<Control> InsideProperty = AvaloniaProperty.Register<Block, Control>(nameof(Inside));
  public static readonly StyledProperty<Control> OutsideProperty = AvaloniaProperty.Register<Block, Control>(nameof(Outside));

  public Control Inside
  {
    get { return GetValue(InsideProperty); }
    set { SetValue(InsideProperty, value); }
  }

  public Control Outside
  {
    get { return GetValue(OutsideProperty); }
    set { SetValue(OutsideProperty, value); }
  }
}
