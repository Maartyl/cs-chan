using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanReceiverBase<T> : NetChanTBase<T>, IChanReceiver<T> {
    protected NetChanReceiverBase(Stream netIn, Stream netOut, NetChanConfig<T> cfg):base(netIn, netOut, cfg) {

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
      throw new NotImplementedException();
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

