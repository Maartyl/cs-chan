using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanSenderBase<T> : NetChanTBase<T>, IChanSender<T> {
    protected NetChanSenderBase(Stream netIn, Stream netOut, NetChanConfig<T> cfg):base(netIn, netOut, cfg) {

    }
    #region implemented abstract members of NetChanBase
    protected override Task<Header> OnMsgReceived(Header h) {
      throw new NotImplementedException();
    }

    protected override Task OnCloseReceived(Header h) {
      throw new NotImplementedException();
    }
    #endregion
    #region IChanSender implementation
    public Task SendAsync(T msg) {
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

