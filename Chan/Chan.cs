using System.Threading.Tasks;
using System;

namespace Chan
{
  public static class Chan {
    //problem with pipeing: exceptions are not propagated, instead thrown in this task
    //this completes with close of either first (cancel) or both (if propagates) channels.
    public static Task Pipe<T>(this IChanReceiver<T> rchan, IChanSender<T> schan, bool propagateClose = true) {
      return Pipe(rchan, schan, x => x, propagateClose);
    }

    public static async Task Pipe<TR, TS>(this IChanReceiver<TR> rchan, IChanSender<TS> schan,
                                          Func<TR,TS> fmap, bool propagateClose = true) {
      try {
        while (true)
          await rchan.ReceiveAsync(v => schan.SendAsync(fmap(v)));
      } catch (TaskCanceledException) {
        //pass
      }
      if (propagateClose) 
        await Task.WhenAll(schan.Close(), rchan.Close());
    }

    public static ChanFactory<T, Nothing> FactorySimple<T>(IChanReceiver<T> rchan, IChanSender<T> schan) {
      return new ChanFactoryWrap<T>(rchan, schan);
    }

    public static ChanFactory<T, Nothing> FactoryBroadcast<T>(IChanReceiver<T> rchan, IChanSender<T> schan) {
      return new ChanFactoryReceiveAll<T>(rchan, schan);
    }

    public static ChanFactory<T, Nothing> FactoryFor<T>(ChanDistributionType dt, IChanReceiver<T> rchan, IChanSender<T> schan) {
      switch (dt) {
        case ChanDistributionType.Broadcast:
          return FactoryBroadcast(rchan, schan);
        case ChanDistributionType.FirstOnly:
          return FactorySimple(rchan, schan);
        default:
          throw new NotImplementedException("unexpected ChanDistributionType:" + dt);
      }
    }

    public static Func<IChanReceiver<T>, IChanSender<T>, ChanFactory<T, Nothing>> FactoryFor<T>(ChanDistributionType dt) {
      return (a, b) => FactoryFor(dt, a, b);
    }

    public static IChan<T> Closed<T>() {
      return new ClosedChan<T>();
    }

    private class ClosedChan<T> : IChan<T> {
      #region IChanReceiver implementation
      public Task<T> ReceiveAsync() {
        var ts = new TaskCompletionSource<T>();
        ts.SetCanceled();
        return ts.Task;
      }

      public Task<T> ReceiveAsync(Func<T, Task> sendResult) {
        return ReceiveAsync();
      }
      #endregion
      #region IChanSender implementation
      public Task SendAsync(T msg) {
        return ReceiveAsync();
      }
      #endregion
      #region IChanBase implementation
      public Task Close() {
        return Task.Delay(0);
      }

      public Task AfterClosed() {
        return Task.Delay(0);
      }
      #endregion
    }
  }
}

