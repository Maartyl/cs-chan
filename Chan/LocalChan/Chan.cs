using System;
using System.Threading.Tasks;

namespace Chan
{
  //this partially handles closing of channel, not synchronization
  public abstract class Chan<TMsg> : IChan<TMsg> {
    //wraps result of first call to CloseOnce
    readonly TaskCompletionSource<Task> closingTaskPromise = new TaskCompletionSource<Task>();

    public bool Closed { get { return closingTaskPromise.Task.IsCompleted; } }

    public virtual Task Close() {
      if (!Closed)
        lock (closingTaskPromise)
          if (!Closed) 
            closingTaskPromise.SetResult(CloseOnce());
      return AfterClosed();
    }

    ///only called once
    protected virtual Task CloseOnce() {
      return Task.Delay(0);
    }

    public Task AfterClosed() {
      return closingTaskPromise.Task.Flatten();
    }

    ///after Closing channel: returns if all messages have been "received"
    /// (after close:) after true once, it should never be false again
    protected abstract bool NoMessagesLeft();

    public Task<TMsg> ReceiveAsync(Func<TMsg, Task> sendCallback) {
      if (Closed)
        return ReceiveAsyncCancelled(sendCallback, CancelledTask);
      return ReceiveAsyncImpl(sendCallback);
    }

    public Task<TMsg> ReceiveAsync() {
      return ReceiveAsync(x => Task.Delay(0));
    }

    protected abstract Task<TMsg> ReceiveAsyncImpl(Func<TMsg, Task> sendCallback);

    protected virtual Task<TMsg> ReceiveAsyncCancelled(Func<TMsg, Task> sendCallback, Task<TMsg> cancelled) {
      if (NoMessagesLeft())
        return cancelled;
      return ReceiveAsyncImpl(sendCallback);
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

