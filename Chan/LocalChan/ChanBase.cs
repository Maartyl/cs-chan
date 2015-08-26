using System;
using System.Threading.Tasks;

namespace Chan
{
  //this partially handles closing of channel, not synchronization
  //base for local chans
  public abstract class ChanBase<TMsg> : IChan<TMsg> {
    #region close

    readonly InvokeOnceEmbeddable closing;

    protected ChanBase() {
      closing = new InvokeOnceEmbeddable(CloseOnce);
    }

    public bool Closed { get { return closing.Invoked; } }

    public Task Close() {
      return closing.Invoke();
    }

    public Task AfterClosed() {
      return closing.AfterInvoked;
    }

    ///only called once
    protected virtual Task CloseOnce() {
      return Task.Delay(0);
    }

    #endregion

    ///after Closing channel: returns if all messages have been "received"
    /// (after close:) after true once, it should never be false again
    protected abstract bool NoMessagesLeft();

    public Task<TMsg> ReceiveAsync(Func<TMsg, Task> sendCallback) {
      return Closed 
        ? ReceiveAsyncCancelled(sendCallback, CancelledTask) 
          : ReceiveAsyncImpl(sendCallback);
    }

    public Task<TMsg> ReceiveAsync() {
      return ReceiveAsync(x => Task.Delay(0));
    }

    protected abstract Task<TMsg> ReceiveAsyncImpl(Func<TMsg, Task> sendCallback);

    protected virtual Task<TMsg> ReceiveAsyncCancelled(Func<TMsg, Task> sendCallback, Task<TMsg> cancelled) {
      return NoMessagesLeft() ? cancelled 
          : ReceiveAsyncImpl(sendCallback);
    }

    public Task SendAsync(TMsg msg) {
      return Closed ? CancelledTask : SendAsyncImpl(msg);
    }

    protected abstract Task SendAsyncImpl(TMsg msg);

    protected readonly static Task<TMsg> CancelledTask;

    static ChanBase() {
      //1 cached cancelled task 
      var tcs = new TaskCompletionSource<TMsg>();
      tcs.SetCanceled();
      CancelledTask = tcs.Task;
    }
  }
}

