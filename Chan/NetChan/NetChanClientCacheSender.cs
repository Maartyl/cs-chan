using System;

namespace Chan
{
  internal abstract class NetChanClientCacheSender : NetChanClientCache<IChanSenderFactory<Unit>> {
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

    protected override IChanSenderFactory<Unit> RequireConnect(System.Net.Sockets.TcpClient c, NetChanConnectionInfo info, Uri chan) {
      //assert info.IsOk == true
      var s = c.GetStream();
      var client = new NetChanSenderClient<T>(defaultConfig.Clone(s, s));
      clientStarts.Add(client.Start(info.Key));

      var noReceiver = new ChanAsync<T>();
      //cannot use null, because broadcast factory uses ChanEvent which requires chan to wait on
      return Chan.FactoryFor(info.Type, noReceiver, client);
    }
  }
}

