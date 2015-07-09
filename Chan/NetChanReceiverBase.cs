using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanReceiverBase<T> : NetChanTBase<T>, IChanReceiver<T> {
    protected NetChanReceiverBase(NetChanConfig<T> cfg):base(cfg) {

    }

    protected override Task<Header> OnMsgReceived(Header h) {
      throw new NotImplementedException();
    }

    protected override Task OnCloseReceived(Header h) {
      throw new NotImplementedException();
    }

    public Task<T> ReceiveAsync() {
      throw new NotImplementedException();
    }

    protected override Task CloseOnce() {
      throw new NotImplementedException();
    }
  }
}

