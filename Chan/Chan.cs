using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Channels
{
  //this partially handles closing of channel, not synchronization
  public abstract class Chan<TMsg> : IChanBoth<TMsg> {
    protected bool Closed { get; private set; }

    protected Chan() {
      Closed = false;
    }

    public virtual void Close() {
      Closed = true;
    }

    protected abstract bool NoRemainingMessagesAfterClosed();

    public Task<TMsg> ReceiveAsync() {
      if (Closed && NoRemainingMessagesAfterClosed())
        return CancelledTask;
      return ReceiveAsyncImpl();
    }

    protected abstract Task<TMsg> ReceiveAsyncImpl();

    public Task SendAsync(TMsg msg) {
      if (Closed)
        return CancelledTask;
      return SendAsyncImpl(msg);
    }

    protected abstract Task SendAsyncImpl(TMsg msg);

    protected readonly static Task<TMsg> CancelledTask;

    static Chan() {
      var tcs = new TaskCompletionSource<TMsg>();
      tcs.SetCanceled();
      CancelledTask = tcs.Task;
    }
  }
}

