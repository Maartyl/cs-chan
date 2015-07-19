using System.Threading.Tasks;
using System;

namespace Chan
{
  public class NetChanSenderServer<T> : NetChanSenderBase<T> {
    public NetChanSenderServer(NetChanConfig<T> cfg):base(cfg) {
      DbgCns.Trace(this, ".ctor");
    }

    public override async Task Start(uint key) {
      DbgCns.Trace(this, "start0");
      await HandshakeServer(key);
      DbgCns.Trace(this, "start1");
      await StartSender();
      DbgCns.Trace(this, "start2");
    }
  }
}

