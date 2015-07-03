using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Chan
{
  //this partially handles closing of channel, not synchronization
  public abstract class Chan<TMsg> : IChanBoth<TMsg> {
    protected bool Closed { get; private set; }

    protected readonly ConcurrentQueue<TMsg> Q = new ConcurrentQueue<TMsg>();

    protected Chan() {
      Closed = false;
    }
    #region IChan implementation
    public void Close() {
      Closed = true;
    }
    #endregion
    #region IChanReceiver implementation
    public Task<TMsg> ReceiveAsync() {
      if (Closed && Q.IsEmpty)
        return CancelledTask;
      return ReceiveAsyncImpl();
    }

    protected abstract Task<TMsg> ReceiveAsyncImpl();
    #endregion
    #region IChanSender implementation
    public Task SendAsync(TMsg msg) {
      if (Closed)
        return CancelledTask;
      return SendAsyncImpl(msg);
    }

    protected abstract Task SendAsyncImpl(TMsg msg);
    #endregion
    protected readonly static Task<TMsg> CancelledTask;

    static Chan() {
      var tcs = new TaskCompletionSource<TMsg>();
      tcs.SetCanceled();
      CancelledTask = tcs.Task;
    }
  }
}

