using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Chan
{
  public sealed class ChanAsync<T> : ChanBase<T> {
    readonly BlockingCollection<TaskCompletionCallback<T, Task>> promises;
    readonly BlockingCollection<DeliverAsync<T>> waiters;

    public ChanAsync(int receiveTaskCountLimit, int sendTaskCountLimit) { 
      promises = new BlockingCollection<TaskCompletionCallback<T, Task>>(receiveTaskCountLimit);
      waiters = new BlockingCollection<DeliverAsync<T>>(sendTaskCountLimit);
    }

    public ChanAsync() : this(500, 500) {
    }

    #region implemented abstract members of Chan

    protected override bool NoMessagesLeft() {
      return waiters.Count == 0;
    }

    protected override Task<T> ReceiveAsyncImpl(Func<T, Task> sendCallback) {
      DeliverAsync<T> da;
      lock (waiters)
        if (waiters.TryTake(out da)) {
          DebugCounter.Incg(this, "r.w");
          DbgCns.Trace(this, "r.w");
          return Task.FromResult(da.Deliver(sendCallback));
        } else {
          DebugCounter.Incg(this, "r.p");
          DbgCns.Trace(this, "r.p");
          var p = new TaskCompletionCallback<T, Task>(sendCallback);
          promises.Add(p);
          return p.Task;
        }
    }

    protected override Task<T> ReceiveAsyncCancelled(Func<T, Task> sendCallback, Task<T> cancelled) {
      DeliverAsync<T> da;
      lock (waiters)
        if (waiters.TryTake(out da)) {
          DebugCounter.Incg(this, "c.w");
          DbgCns.Trace(this, "c.w");
          return Task.FromResult(da.Deliver(sendCallback));
        } else {
          DebugCounter.Incg(this, "c.c");
          DbgCns.Trace(this, "c.c");
          return cancelled;
        }
    }

    protected override Task SendAsyncImpl(T msg) {
      TaskCompletionCallback<T, Task> p;
      lock (waiters)
        if (promises.TryTake(out p)) {
          DebugCounter.Incg(this, "s.p");
          DbgCns.Trace(this, "s.p");
          return p.SetResult(msg);
        } else {
          DebugCounter.Incg(this, "s.w");
          DbgCns.Trace(this, "s.w");
          return DeliverAsync.Start(msg, waiters.Add);
        }
    }

    protected async override Task CloseOnce() {
      DbgCns.Trace(this, "close-once0");
      //await Task.Yield();
      waiters.CompleteAdding();

      //wait for calls to receive; until all waiters gone
      var waitTime = 3;
      while (!NoMessagesLeft())
        await Task.Delay(waitTime = (int) (waitTime * 1.8));

      TaskCompletionCallback<T, Task> p; //cancel all promises that cannot be delivered
      while (promises.TryTake(out p)) {
        DebugCounter.Incg(this, "e.p");
        p.SetCanceled();
      }
      DbgCns.Trace(this, "close-onceE");
    }

    #endregion
  }
}

