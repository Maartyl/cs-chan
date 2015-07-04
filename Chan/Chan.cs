using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Chan
{
  //this partially handles closing of channel, not synchronization
  public abstract class Chan<TMsg> : IChanBoth<TMsg> {
    volatile bool closed = false;
    //wraps result of first call to CloseImpl
    volatile TaskCompletionSource<Task> closingTaskPromise = new TaskCompletionSource<Task>();

    protected bool Closed { get { return closed; } }

    public virtual async Task Close() {
      if (!closed) {
        closed = true;
        closingTaskPromise.SetResult(CloseImpl());
      }
      await await closingTaskPromise.Task;
    }

    ///only called once
    protected virtual Task CloseImpl() {
      return Task.Delay(0);
    }

    public async Task AfterClosed() {
      await await closingTaskPromise.Task;
    }

    ///after Closing channel: returns if all messages have been "received"
    /// (after close:) after true once, it should never be false again
    protected abstract bool NoMessagesLeft();

    public Task<TMsg> ReceiveAsync() {
      if (Closed)
        return ReceiveAsyncCancelled(CancelledTask);
      return ReceiveAsyncImpl();
    }

    protected abstract Task<TMsg> ReceiveAsyncImpl();

    protected virtual Task<TMsg> ReceiveAsyncCancelled(Task<TMsg> cancelled) {
      if (NoMessagesLeft())
        return cancelled;
      return ReceiveAsyncImpl();
    }

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

