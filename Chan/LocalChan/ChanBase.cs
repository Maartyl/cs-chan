using System;
using System.Threading.Tasks;
using System.Threading;

namespace Chan
{
  //this partially handles closing of channel, not synchronization
  //base for local chans
  public abstract class ChanBase<TMsg> : IChan<TMsg> {
    #region close

    //wraps result of first call to CloseOnce
    readonly TaskCompletionSource<Task> closingTaskPromise = new TaskCompletionSource<Task>();
    //cannot use closingTask.Promise..IsCompleted: set AFTER running CloseOnce
    // - I have to set right away
    ///bool but need Interlocked (0=false; otherwise true)
    int isClosed = 0;

    public bool Closed { get { return isClosed != 0; } }

    /// retVal == this call changed to closed
    bool SetClosed() {
      return 0 == Interlocked.Exchange(ref isClosed, 1);
    }

    public Task Close() {
      if (!Closed && SetClosed())
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

    #endregion

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

    static ChanBase() {
      var tcs = new TaskCompletionSource<TMsg>();
      tcs.SetCanceled();
      CancelledTask = tcs.Task;
    }
  }
}

