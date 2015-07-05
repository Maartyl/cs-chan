using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Chan
{
  public class ChanAsync<T> : Chan<T> {
    readonly BlockingCollection<TaskCompletionSource<T>> promises;
    readonly BlockingCollection<DeliverAsync<T>> waiters;

    public ChanAsync(int promisesMax, int waitersMax) { 
      promises = new BlockingCollection<TaskCompletionSource<T>>(promisesMax);
      waiters = new BlockingCollection<DeliverAsync<T>>(waitersMax);
    }

    public ChanAsync():this(500,500) {
    }
    #region implemented abstract members of Chan
    protected override bool NoMessagesLeft() {
      return waiters.Count == 0;
    }

    protected override Task<T> ReceiveAsyncImpl() {
      DeliverAsync<T> da;
      lock (waiters)
        if (waiters.TryTake(out da)) {
          DebugCounter.Incg(this, "r.w");
          return Task.FromResult(da.Deliver());
        } else {
          DebugCounter.Incg(this, "r.p");
          var p = new TaskCompletionSource<T>();
          promises.Add(p);
          return p.Task;
        }
    }

    protected override Task<T> ReceiveAsyncCancelled(Task<T> cancelled) {
      DeliverAsync<T> da;
      lock (waiters)
        if (waiters.TryTake(out da)) {
          DebugCounter.Incg(this, "c.w");
          return Task.FromResult(da.Deliver());
        } else {
          DebugCounter.Incg(this, "c.c");
          return cancelled;
        }
    }

    protected override Task SendAsyncImpl(T msg) {
      TaskCompletionSource<T> p;
      lock (waiters)
        if (promises.TryTake(out p)) {
          DebugCounter.Incg(this, "s.p");
          p.SetResult(msg);
          return Task.Delay(0);
        } else {
          DebugCounter.Incg(this, "s.w");
          return DeliverAsync.Start(msg, waiters.Add);
        }
    }

    protected async override Task CloseImpl() {
      await Task.Delay(0);
      waiters.CompleteAdding();
      while (!NoMessagesLeft())
        await Task.Delay(5);//wait for calls to receive, until all waiters gone

      TaskCompletionSource<T> p;
      while (promises.TryTake(out p)) {
        DebugCounter.Incg(this, "e.p");
        p.SetCanceled();
      }
    }
    #endregion
  }
}

