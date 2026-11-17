using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BTokenGUI.ViewModels;
using BTokenGUI.Views;

namespace BTokenGUI;

public partial class App : Application
{
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
#if DEBUG
    this.AttachDeveloperTools();
#endif
  }

  public override void OnFrameworkInitializationCompleted()
  {
    MainViewModel mainViewModel = new();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      desktop.MainWindow = new MainWindow { DataContext = mainViewModel };
    }
    else if (ApplicationLifetime is IActivityApplicationLifetime activity)
    {
      activity.MainViewFactory = () => new MainViewMobile { DataContext = mainViewModel };
    }

    base.OnFrameworkInitializationCompleted();
  }
}
