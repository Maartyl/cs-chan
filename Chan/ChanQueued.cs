using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace Channels
{
  /// <summary>
  /// All messages pass through concurrent queue...
  /// </summary>
  public class ChanQueued<TMsg> : Chan<TMsg> {
    readonly ConcurrentQueue<TMsg> Q = new ConcurrentQueue<TMsg>();
    readonly ConcurrentQueue<TaskCompletionSource<TMsg>> promises = new ConcurrentQueue<TaskCompletionSource<TMsg>>();
    readonly ConcurrentQueue<ManualResetEventSlim> waiters = new ConcurrentQueue<ManualResetEventSlim>();
    //blocked threads: queue soft limit : it is possible to be a little bit bigger
    readonly int qlimit;

    /// <summary>
    /// Initializes a new instance of the <see cref="Chan.ChanQueued`1"/> class with queue limit: 1.
    /// </summary>
    public ChanQueued():this(1) {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Chan.ChanQueued`1"/> class.
    /// </summary>
    /// <param name="queueSizeSoftLimit">Queue size - soft limit.</param>
    public ChanQueued(int queueSizeSoftLimit) {
      if (queueSizeSoftLimit < 1)
        throw new ArgumentException("Channel queue limit must be >=1 (was: " + queueSizeSoftLimit + ")", "queueSizeSoftLimit");
      qlimit = queueSizeSoftLimit;
    }

    protected override Task<TMsg> ReceiveAsyncImpl() {
      Task<TMsg> rslt;
      TMsg msg;

      if (Q.TryDequeue(out msg)) {
        rslt = Task.FromResult(msg);
      } else {//nothing ready
        tryEnqueueWaiting();
        if (Q.TryDequeue(out msg)) {
          rslt = Task.FromResult(msg);
        } else {//only promise
          var tcs = new TaskCompletionSource<TMsg>();
          promises.Enqueue(tcs);
          rslt = tcs.Task;
        }
      }
      tryEnqueueWaiting();
      return rslt;
    }

    protected override Task SendAsyncImpl(TMsg msg) {
      if (Q.Count < qlimit) {
        Q.Enqueue(msg);
        tryDeliverToPromises();
        return Task.WhenAll(); //completed task
      } else {
        //block thread
        var mre = new ManualResetEventSlim();
        waiters.Enqueue(mre);
        mre.Wait();
        return SendAsyncImpl(msg);
      }
    }

    void tryEnqueueWaiting() {
      while (Q.Count < qlimit) {
        ManualResetEventSlim mre;
        if (waiters.TryDequeue(out mre)) {
          mre.Set();
          mre.Dispose();
        } else
          return;
      }
    }

    void tryDeliverToPromises() {
      while (!(promises.IsEmpty || Q.IsEmpty)) {
        TaskCompletionSource<TMsg> p;
        if (promises.TryDequeue(out p)) {
          TMsg msg;
          if (Q.TryDequeue(out msg)) {
            p.SetResult(msg);
          } else {
            //DANGER: I dequeued promise, but cannot deliver: re-enqueue
            //this changes order, but without prepending, there's no better option (only absolutely crazy)
            promises.Enqueue(p);
            return;
          }
        } else {
          return;
        }
      }
    }

    public override void Close() {
      base.Close();
      //push rest of waiters and fill promises
      //!!! promises that cennot be delivered must be cancelled
    }

    protected override bool NoRemainingMessagesAfterClosed() {
      return Q.IsEmpty && waiters.IsEmpty;
    }
  }
}

