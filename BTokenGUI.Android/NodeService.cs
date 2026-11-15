using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using BTokenCore;

namespace BTokenGUI.Android;

[Register("com.btoken.gui.NodeService")]
public class NodeService : Service
{
  const int PORT_RPC = 8778;
  const int ID_NOTIFICATION = 1;
  const string ID_CHANNEL = "node";

  static Task TaskNode;


  public override IBinder OnBind(Intent intent)
  {
    return null;
  }

  public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
  {
    NotificationManager notificationManager = (NotificationManager)GetSystemService(NotificationService);
    notificationManager.CreateNotificationChannel(new NotificationChannel(ID_CHANNEL, "BToken node", NotificationImportance.Low));

    Notification notification = new Notification.Builder(this, ID_CHANNEL)
      .SetContentTitle("BToken node running")
      .SetSmallIcon(global::Android.Resource.Drawable.StatNotifySync)
      .SetOngoing(true)
      .Build();

    StartForeground(ID_NOTIFICATION, notification);

    TaskNode ??= Task.Run(RunNode);

    return StartCommandResult.Sticky;
  }

  async Task RunNode()
  {
    Directory.SetCurrentDirectory(FilesDir.AbsolutePath);

    Node node = new(new CommunicationTCP());

    await node.Start();

    await new RPCServer(node, PORT_RPC).Start();
  }
}
