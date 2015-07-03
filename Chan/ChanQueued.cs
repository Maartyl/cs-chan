using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace Chan
{
  /// <summary>
  /// All messages pass through queue...
  /// </summary>
  public class ChanSimple<TMsg> : Chan<TMsg> {
    ConcurrentQueue<TaskCompletionSource<TMsg>> promises = new ConcurrentQueue<TaskCompletionSource<TMsg>>();
    ConcurrentQueue<WaitHandle> waiters = new ConcurrentQueue<WaitHandle>();
    //blocked threads: queue limit
    readonly int qlimit;

    public ChanSimple():this(1) {

    }

    public ChanSimple(int queueSizeSoftLimit) {
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
          tryEnqueueWaiting();
          var tcs = new TaskCompletionSource<TMsg>();
        }
      }
      tryEnqueueWaiting();
      return rslt;
    }

    protected override async Task SendAsyncImpl(TMsg msg) {

    }

    private void tryEnqueueWaiting() {
      if (Q.Count >= qlimit || waiters.IsEmpty)
        return;



      tryEnqueueWaiting();
    }
  }
}

