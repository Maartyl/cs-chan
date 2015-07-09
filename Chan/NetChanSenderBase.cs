using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanSenderBase<T> : NetChanTBase<T>, IChanSender<T> {
    protected NetChanSenderBase(NetChanConfig<T> cfg):base(cfg) {

    }

    protected async Task PipeWorlSend() {
      //assures there are no 2 messages being sent at the same time
      // - could happen if sending was accesible directly
      try {
        while (true) 
          await SendMsg(await World.ReceiveAsync());
      } catch (TaskCanceledException ex) {
        return; //ok done
      }
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
      return World.SendAsync(msg);
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

