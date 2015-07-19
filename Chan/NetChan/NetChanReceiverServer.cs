using System.Threading.Tasks;
using System;

namespace Chan
{
  public class NetChanReceiverServer<T> : NetChanReceiverBase<T> {
    public NetChanReceiverServer(NetChanConfig<T> cfg):base(cfg) {
      DbgCns.Trace(this, ".ctor");
    }

    public override async Task Start(uint key) {
      DbgCns.Trace(this, "start0");
      await HandshakeServer(key);
      DbgCns.Trace(this, "start1");
      await StartReceiver();
      DbgCns.Trace(this, "startE");
    }
  }
}

