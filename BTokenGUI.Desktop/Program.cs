using System;
using System.Diagnostics;
using System.IO;
using Avalonia;

namespace BTokenGUI.Desktop;

sealed class Program
{
  const string NAME_CONSOLE_HOST = "ConsoleHost";


  [STAThread]
  public static void Main(string[] args)
  {
    Process consoleHost = StartConsoleHost();

    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

#if DEBUG
    consoleHost?.Kill(true);
#endif
  }

  static Process StartConsoleHost()
  {
    if (Process.GetProcessesByName(NAME_CONSOLE_HOST).Length > 0)
      return null;

    string directoryNode = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BToken");
    Directory.CreateDirectory(directoryNode);

    return Process.Start(new ProcessStartInfo
    {
      FileName = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? NAME_CONSOLE_HOST + ".exe" : NAME_CONSOLE_HOST),
      WorkingDirectory = directoryNode,
      UseShellExecute = false,
      CreateNoWindow = true
    });
  }

  public static AppBuilder BuildAvaloniaApp()
  {
    return AppBuilder.Configure<App>()
      .UsePlatformDetect()
      .WithInterFont()
      .LogToTrace();
  }
}
