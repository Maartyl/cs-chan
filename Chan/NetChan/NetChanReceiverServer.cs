using System.Threading.Tasks;
using System;

namespace Chan
{
  public class NetChanReceiverServer<T> : NetChanReceiverBase<T> {
    public NetChanReceiverServer(NetChanConfig<T> cfg):base(cfg) {
    }

    public override async Task Start(uint key) {
      await HandshakeServer(key);
      await StartReceiver();
    }
  }
}

