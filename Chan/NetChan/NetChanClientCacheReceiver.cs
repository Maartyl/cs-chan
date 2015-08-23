using System;

namespace Chan
{
  internal abstract class NetChanClientCacheReceiver : NetChanClientCache<IChanReceiverFactory<Unit>> {
    protected NetChanClientCacheReceiver() {
    }

    protected override NetChanConnectionInfo Request(INetChanProvider p, Uri chanLocalUri) {
      return p.RequestReceiver(chanLocalUri);
    }
  }

  internal class NetChanClientCacheReceiver<T> : NetChanClientCacheReceiver {
    readonly NetChanConfig<T> defaultConfig;

    public NetChanClientCacheReceiver(NetChanConfig<T> deafultConfig) {
      this.defaultConfig = deafultConfig;

    }

    protected override IChanReceiverFactory<Unit> RequireConnect(System.Net.Sockets.TcpClient c, NetChanConnectionInfo info, Uri chan) {
      //assert info.IsOk == true
      var s = c.GetStream();
      var client = new NetChanReceiverClient<T>(defaultConfig.Clone(s, s));
      clientStarts.Add(client.Start(info.Key));
      var factory = Chan.FactoryFor(info.Type, client, null/*no sender*/);
      factory.AfterClosed().ContinueWith(t => Forget(chan));
      return factory;
    }
  }
}

