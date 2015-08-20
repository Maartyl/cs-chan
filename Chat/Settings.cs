using System;

namespace Chat
{
  public static class Settings {
    public static int ServerPort{ get { return 4586; } }

    public static char UserCommandStart{ get { return ':'; } }

    public static string ClientDefaultName{ get { return "anon"; } }

    public static string ProtocolId{ get { return "NPRG038CHAT"; } }

    public static string HandshakeProtocolVersion{ get { return "###handshake"; } }
  }
}

