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
    StartConsoleHost();

    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
  }

  static void StartConsoleHost()
  {
    if (Process.GetProcessesByName(NAME_CONSOLE_HOST).Length > 0)
      return;

    string directoryNode = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BToken");
    Directory.CreateDirectory(directoryNode);

    Process.Start(new ProcessStartInfo
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
