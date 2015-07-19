using System.Threading.Tasks;
using System.Threading;
using System;

namespace Chan
{
  public class NetChanReceiverClient<T> : NetChanReceiverBase<T> {
    public NetChanReceiverClient(NetChanConfig<T> cfg):base(cfg) {
      DbgCns.Trace(this, ".ctor");
    }

    public override async Task Start(uint key) {
      DbgCns.Trace(this, "start0");
      await HandshakeClient(key);
      DbgCns.Trace(this, "start1");
      var pT = PingLoop(Token);
      var sT = StartReceiver();
      await Task.WhenAll(pT, sT);
      DbgCns.Trace(this, "startE");
    }
  }
}