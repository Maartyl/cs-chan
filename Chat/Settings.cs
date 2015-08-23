using System;

namespace Chat
{
  public class Settings {

    public Settings() {
      DefaultServerPort = 4567;
    }

    /// -1 == not allowed
    public int DefaultServerPort { get ; set; }



    public string ClientDefaultName{ get { return "anon"; } }

    public static string ChanBroadcastName { get { return "chat/broadcast"; } }

    public static char UserCommandStart{ get { return ':'; } }
  }
}
