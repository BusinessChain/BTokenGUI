using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace BTokenGUI.Android;

[Activity(
  Label = "BToken",
  Theme = "@style/BTokenTheme",
  MainLauncher = true,
  ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
