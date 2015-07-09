using System.Threading.Tasks;
using System;

namespace Chan
{
  public class NetChanSenderClient<T> : NetChanSenderBase<T> {
    public NetChanSenderClient(NetChanConfig<T> cfg):base(cfg) {
    }

    public override async Task Start(uint key) {
      await HandshakeClient(key);
      await StartSender();
    }
  }
}

