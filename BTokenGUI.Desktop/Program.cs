using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;

namespace BTokenGUI.Desktop;

sealed class Program
{
  const string NAME_CONSOLE_HOST = "ConsoleHost";

#if DEBUG
  const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
  const int JOB_OBJECT_EXTENDED_LIMIT_INFORMATION_CLASS = 9;

  static IntPtr JobConsoleHost;

  [StructLayout(LayoutKind.Sequential)]
  struct JobObjectBasicLimitInformation
  {
    public long PerProcessUserTimeLimit;
    public long PerJobUserTimeLimit;
    public uint LimitFlags;
    public UIntPtr MinimumWorkingSetSize;
    public UIntPtr MaximumWorkingSetSize;
    public uint ActiveProcessLimit;
    public UIntPtr Affinity;
    public uint PriorityClass;
    public uint SchedulingClass;
  }

  [StructLayout(LayoutKind.Sequential)]
  struct IOCounters
  {
    public ulong ReadOperationCount;
    public ulong WriteOperationCount;
    public ulong OtherOperationCount;
    public ulong ReadTransferCount;
    public ulong WriteTransferCount;
    public ulong OtherTransferCount;
  }

  [StructLayout(LayoutKind.Sequential)]
  struct JobObjectExtendedLimitInformation
  {
    public JobObjectBasicLimitInformation BasicLimitInformation;
    public IOCounters IoInfo;
    public UIntPtr ProcessMemoryLimit;
    public UIntPtr JobMemoryLimit;
    public UIntPtr PeakProcessMemoryUsed;
    public UIntPtr PeakJobMemoryUsed;
  }

  [DllImport("kernel32.dll")]
  static extern IntPtr CreateJobObject(IntPtr jobAttributes, string name);

  [DllImport("kernel32.dll")]
  static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref JobObjectExtendedLimitInformation info, uint length);

  [DllImport("kernel32.dll")]
  static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
#endif


  [STAThread]
  public static void Main(string[] args)
  {
    Process consoleHost = StartConsoleHost();

#if DEBUG
    if (consoleHost != null && OperatingSystem.IsWindows())
      BindToLifetimeOfGUI(consoleHost);
#endif

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

#if DEBUG
  static void BindToLifetimeOfGUI(Process consoleHost)
  {
    JobConsoleHost = CreateJobObject(IntPtr.Zero, null);

    JobObjectExtendedLimitInformation info = new();
    info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

    SetInformationJobObject(
      JobConsoleHost,
      JOB_OBJECT_EXTENDED_LIMIT_INFORMATION_CLASS,
      ref info,
      (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>());

    AssignProcessToJobObject(JobConsoleHost, consoleHost.Handle);
  }
#endif

  public static AppBuilder BuildAvaloniaApp()
  {
    return AppBuilder.Configure<App>()
      .UsePlatformDetect()
      .WithInterFont()
      .LogToTrace();
  }
}
