using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace Chan
{
  /// <summary>
  /// All messages pass through concurrent queue...
  /// Order is guaranteed if only 1 receiveAsync is waited for at a time
  /// (actually maybe not really: not everything necessarily uses promise queue...)
  /// </summary>
  //TODO: possible improvement: use BlockingCollection over ConcurentQueue Q : takes care of waiters logic
  // - also provides bounding: qlimit (... it really goes hand in hand...)
  //the order is: promises -> Q -> waiters
  //~Proven parts:
  //Order always correct if: receiver waits for sender (no matter how many receivers: in order of calling receiveAsync)
  //Order always correct if: all fits into queue (no waiting)
  //Cannot have both promise and queue items if: only 1 in, only 1 out
  // - that is currently only thing to possibly cause wrong order
  // - happens very rarely: probably just puting a lock around it is good enough
  // -- sadly: lock probably also needed around something common...
  //OK: I WILL NOT SOLVE THIS PROBLEM WITH ORDER - possibly: another version which has lock, but is slower
  // - This is only problem if: 2+ receive or 2+ send at the exactly same time, when no-one waiting and empty promises
  // - It might change order of messages OTHER then the 2 "accessed" simultaneously
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
        tryEnqueueWaiting(); //made room
        DebugCounter.Incg(this, "r.q");
      } else {//nothing ready
//        tryEnqueueWaiting();
//        if (Q.TryDequeue(out msg)) {
//          rslt = Task.FromResult(msg);
//        } else {//only promise
        var tcs = new TaskCompletionSource<TMsg>();
        promises.Enqueue(tcs);
        rslt = tcs.Task;
        DebugCounter.Incg(this, "r.p");
//        }
      }

      return rslt;
    }

    protected override Task<TMsg> ReceiveAsyncCancelled(Task<TMsg> cancelled) {
      Task<TMsg> rslt;
      TMsg msg;

      if (Q.TryDequeue(out msg)) {
        rslt = Task.FromResult(msg);
        DebugCounter.Incg(this, "c.q");
        tryEnqueueWaiting(); //made room
      } else {//nothing ready
        DebugCounter.Incg(this, "c.e");
        if (waiters.IsEmpty)
          return cancelled;
        tryEnqueueWaiting();
        return ReceiveAsyncCancelled(cancelled);
      }

      return rslt;
    }

    protected override Task SendAsyncImpl(TMsg msg) {
      TaskCompletionSource<TMsg> p;
      //try: deliver promise right away if possible
      if (promises.TryDequeue(out p)) { 
        p.SetResult(msg);
        DebugCounter.Incg(this, "s.p");
        return Task.Delay(0); //completed task
      } 

      if (Q.Count < qlimit) {//has room to queue msg
        Q.Enqueue(msg);
        DebugCounter.Incg(this, "s.q");
        tryDeliverToPromises();
        return Task.Delay(0); //completed task
      } 
      DebugCounter.Incg(this, "s.w");
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
          DebugCounter.Incg(this, "enqueue.w");
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
            DebugCounter.Incg(this, "deliver");
            p.SetResult(msg);
          } else {
            //DANGER: I dequeued promise, but cannot deliver: re-enqueue
            //this changes order, but without prepending, there's no better option (only absolutely crazy)
            DebugCounter.Incg(this, "wrong-order");
            promises.Enqueue(p);
            return;
          }
        } else {
          return;
        }
      }
    }

    /// <summary>
    /// Completes when remaining receiveTasks (promises) are either delivered or canceled
    /// </summary>
    protected override async Task CloseImpl() {
      DebugCounter.Incg(this, "closing.start");
      //push rest of waiters and fill promises
      //!!! promises that cennot be delivered must be cancelled
      tryEnqueueWaiting();
      tryDeliverToPromises();
      int delayTime = 5;

      /*if (waiters.Count < 20) {
        //... I cannot... - I don't know the value...
      } else*/
      {
        while (!waiters.IsEmpty) {
          await Task.Delay(delayTime += 5);
          DebugCounter.Incg(this, "closing.w");
          tryEnqueueWaiting();
          tryDeliverToPromises();
        }
      }

      while (!Q.IsEmpty) {
        await Task.Delay(delayTime += 5);
        DebugCounter.Incg(this, "closing.q");
        tryDeliverToPromises();
      }
      DebugCounter.Incg(this, "closed");
      //cancell remaning promises : there will never be messages to fill them
      TaskCompletionSource<TMsg> tcs;
      while (promises.TryDequeue(out tcs))
        tcs.SetCanceled();
    }

    protected override bool NoMessagesLeft() {
      return Q.IsEmpty && waiters.IsEmpty;
    }
  }
}

