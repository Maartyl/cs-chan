using System.Threading.Tasks;
using System.Threading;
using System;

namespace Chan
{
  public class NetChanSenderClient<T> : NetChanSenderBase<T> {
    public NetChanSenderClient(NetChanConfig<T> cfg):base(cfg) {
    }

    public override async Task Start(uint key) {
      await HandshakeClient(key);
      var pT = PingLoop(Token);
      var sT = StartSender();
      await Task.WhenAll(pT, sT);
    }
  }
}

