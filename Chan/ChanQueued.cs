using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace Channels
{
  /// <summary>
  /// All messages pass through concurrent queue...
  /// Order is guaranteed if only 1 receiveAsync is waited for at a time
  /// (actually maybe not really: not everything necessarily uses promise queue...)
  /// </summary>
  //TODO: possible improvement: use BlockingCollection over ConcurentQueue Q : takes care of waiters logic
  // - also provides bounding: qlimit (... it really goes hand in hand...)
  public class ChanQueued<TMsg> : Chan<TMsg> {
    readonly ConcurrentQueue<TMsg> Q = new ConcurrentQueue<TMsg>();
    readonly ConcurrentQueue<TaskCompletionSource<TMsg>> promises = new ConcurrentQueue<TaskCompletionSource<TMsg>>();
    //there cannot be too many waiters: bound by number of threads
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
      TaskCompletionSource<TMsg> p;
      //try: deliver promise right away if possible
      if (promises.TryDequeue(out p)) { 
        p.SetResult(msg);
        return Task.WhenAll(); //completed task
      } 

      if (Q.Count < qlimit) {//has room to queue msg
        Q.Enqueue(msg);
        tryDeliverToPromises();
        return Task.WhenAll(); //completed task
      } 

      //block thread
      var mre = new ManualResetEventSlim();
      waiters.Enqueue(mre);
      mre.Wait();
      mre.Dispose();
      return SendAsyncImpl(msg);//recur
    }

    void tryEnqueueWaiting() {
      while (Q.Count < qlimit) {
        ManualResetEventSlim mre;
        if (waiters.TryDequeue(out mre)) {
          mre.Set();
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

    public override async Task Close() {
      await base.Close();
      //push rest of waiters and fill promises
      //!!! promises that cennot be delivered must be cancelled
      tryEnqueueWaiting();
      tryDeliverToPromises();
      await Task.Delay(5); //everything should calm down after a little while
//      if (!waiters.IsEmpty) {
//        var wut = "wut";
//      }

    }

    protected override bool NoMessagesLeft() {
      return Q.IsEmpty && waiters.IsEmpty;
    }
  }
}

