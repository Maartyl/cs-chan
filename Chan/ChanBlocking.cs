using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace Chan
{
  /// <summary>
  /// Channel with no queue: sender is blocked until message is received.
  /// Guarantees, that subsequent calls to Receive will return in the same order as subsequent calls to send.
  /// </summary>
  public class ChanBlocking<TMsg> : Chan<TMsg> {
    readonly ConcurrentQueue<TaskCompletionSource<TMsg>> promises = new ConcurrentQueue<TaskCompletionSource<TMsg>>();
    //there cannot be too many waiters: bound by number of threads
    readonly ConcurrentQueue<DeliverBarrier<TMsg>> waiters = new ConcurrentQueue<DeliverBarrier<TMsg>>();

    protected override Task<TMsg> ReceiveAsyncImpl() {
      DeliverBarrier<TMsg> mse;
      lock (waiters) 
        if (waiters.TryDequeue(out mse)) {
          DebugCounter.Incg(this, "r.w");
          return Task.FromResult(mse.Deliver());
        } else {
          DebugCounter.Incg(this, "r.p");
          var tcs = new TaskCompletionSource<TMsg>();
          promises.Enqueue(tcs);
          return tcs.Task;
        }
    }

    protected override Task<TMsg> ReceiveAsyncCancelled(Task<TMsg> cancelled) {
      DeliverBarrier<TMsg> mse;
      if (waiters.TryDequeue(out mse)) {
        DebugCounter.Incg(this, "c.w");
        return Task.FromResult(mse.Deliver());
      } else {
        DebugCounter.Incg(this, "c.c");
        return cancelled;
      }
    }

    protected override Task SendAsyncImpl(TMsg msg) {
      TaskCompletionSource<TMsg> tcs;
      DeliverBarrier<TMsg> db;
      lock (waiters) 
        if (promises.TryDequeue(out tcs)) {
          DebugCounter.Incg(this, "s.p");
          tcs.SetResult(msg);
          return Task.Delay(0);
        } else {
          DebugCounter.Incg(this, "s.w");
          db = DeliverBarrier.Create(msg);
          waiters.Enqueue(db);
        }
      db.WaitAndDispose();
      return Task.Delay(0);
    }

    protected override async Task CloseOnce() {
      DebugCounter.Incg(this, "closing.start");

      while (!waiters.IsEmpty)
        await Task.Delay(5);
      DebugCounter.Incg(this, "closed");
      TaskCompletionSource<TMsg> tcs;
      while (promises.TryDequeue(out tcs))
        tcs.SetCanceled();
    }

    protected override bool NoMessagesLeft() {
      return waiters.IsEmpty;
    }
  }
}

