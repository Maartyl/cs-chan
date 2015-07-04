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
      if (waiters.TryDequeue(out mse)) {
        return Task.FromResult(mse.Deliver());
      } else {
        var tcs = new TaskCompletionSource<TMsg>();
        promises.Enqueue(tcs);
        return tcs.Task;
      }
    }

    protected override Task SendAsyncImpl(TMsg msg) {
      TaskCompletionSource<TMsg> tcs;
      if (promises.TryDequeue(out tcs)) 
        tcs.SetResult(msg);
      else
        sleep(msg);
      return Task.Delay(0);
    }

    void sleep(TMsg msg) {
      DeliverBarrier.Start(msg, waiters.Enqueue);
    }

    protected override bool NoMessagesLeft() {
      return waiters.IsEmpty;
    }
  }
}

