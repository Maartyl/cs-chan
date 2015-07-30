using System;

namespace Chan
{
  internal abstract class NetChanClientCacheSender : NetChanClientCache<IChanSenderFactory<Nothing>> {
    protected NetChanClientCacheSender() {
    }

    protected override NetChanConnectionInfo Request(INetChanProvider p, Uri chanLocalUri) {
      return p.RequestSender(chanLocalUri);
    }
  }

  internal class NetChanClientCacheSender<T> : NetChanClientCacheSender {
    readonly NetChanConfig<T> defaultConfig;

    public NetChanClientCacheSender(NetChanConfig<T> deafultConfig) {
      this.defaultConfig = deafultConfig;

    }

    protected override IChanSenderFactory<Nothing> RequireConnect(System.Net.Sockets.TcpClient c, NetChanConnectionInfo info, Uri chan) {
      //assert info.IsOk == true
      var s = c.GetStream();
      var client = new NetChanSenderClient<T>(defaultConfig.Clone(s, s));
      clientStarts.Add(client.Start(info.Key));
      return Chan.FactoryFor(info.Type, null/*no receiver*/, client);
    }
  }
}

