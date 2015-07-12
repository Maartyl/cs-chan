using System.Threading.Tasks;
using System;

namespace Chan
{
  public class NetChanSenderServer<T> : NetChanSenderBase<T> {
    public NetChanSenderServer(NetChanConfig<T> cfg):base(cfg) {
    }

    public override async Task Start(uint key) {
      await HandshakeServer(key);
      await StartSender();
    }
  }
}

