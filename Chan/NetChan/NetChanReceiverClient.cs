using System.Threading.Tasks;
using System.Threading;
using System;

namespace Chan
{
  public class NetChanReceiverClient<T> : NetChanReceiverBase<T> {
    public NetChanReceiverClient(NetChanConfig<T> cfg):base(cfg) {
    }

    public override async Task Start(uint key) {
      await HandshakeClient(key);
      var pT = PingLoop(Token);
      var sT = StartReceiver();
      await Task.WhenAll(pT, sT);
    }
  }
}