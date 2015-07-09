using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanReceiverBase<T> : NetChanTBase<T>, IChanReceiver<T> {
    protected NetChanReceiverBase(NetChanConfig<T> cfg):base(cfg) {

    }
    #region implemented abstract members of NetChanBase
    protected override Task<Header> OnMsgReceived(Header h) {
      throw new NotImplementedException();
    }

    protected override Task OnCloseReceived(Header h) {
      throw new NotImplementedException();
    }
    #endregion
    #region IChanReceiver implementation
    public Task<T> ReceiveAsync() {
      return World.ReceiveAsync();
    }
    #endregion
    #region IChanBase implementation
    public Task Close() {
      throw new NotImplementedException();
    }

    public Task AfterClosed() {
      throw new NotImplementedException();
    }
    #endregion
  }
}

