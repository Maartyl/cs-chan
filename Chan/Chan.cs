using System.Threading.Tasks;
using System;

namespace Chan
{
  public static class Chan {

    public static IChan<T> Combine<T>(IChanReceiver<T> r, IChanSender<T> s) {
      return object.ReferenceEquals(r, s) ? (IChan<T>) r : new ChanCombiner<T>(r, s);
    }

    ///cross-wires 2 channels: returning 2 values through a merging function
    public static T FromChanCrossPair<T, TM, TL, TR>(Func<IChanReceiver<TM>, IChanSender<TM>, TL> fl,
                                                     Func<IChanReceiver<TM>, IChanSender<TM>, TR> fr,
                                                     Func<TL, TR, T> f) {
      var c1 = new ChanAsync<TM>(); 
      var c2 = new ChanAsync<TM>();
      return f(fl(c1, c2), fr(c2, c1));
    }
    //this completes with close of either first (cancel) or both (if propagates) channels.
    public static Task Pipe<T>(this IChanReceiver<T> rchan, IChanSender<T> schan, bool propagateClose = true, Action<T> tee = null) {
      return Pipe(rchan, schan, x => x, propagateClose, tee);
    }

    public static async Task Pipe<TR, TS>(this IChanReceiver<TR> rchan, IChanSender<TS> schan,
                                          Func<TR,TS> fmap, bool propagateClose = true, Action<TR> tee = null) {
      if (tee == null)
        tee = x => {
        };
      try {
        while (true)
          tee(await rchan.ReceiveAsync(v => schan.SendAsync(fmap(v))));
      } catch (TaskCanceledException) {
        //pass
      }
      if (propagateClose)
        await Task.WhenAll(schan.Close(), rchan.Close());
    }

    public static ChanFactory<T, Unit> FactorySimple<T>(IChanReceiver<T> rchan, IChanSender<T> schan) {
      return new ChanFactoryWrap<T>(rchan, schan);
    }

    public static ChanFactory<T, Unit> FactoryBroadcast<T>(IChanReceiver<T> rchan, IChanSender<T> schan) {
      return new ChanFactoryReceiveAll<T>(rchan, schan);
    }

    public static ChanFactory<T, Unit> FactoryFor<T>(ChanDistributionType dt, IChanReceiver<T> rchan, IChanSender<T> schan) {
      switch (dt) {
        case ChanDistributionType.Broadcast:
          return FactoryBroadcast(rchan, schan);
        case ChanDistributionType.FirstOnly:
          return FactorySimple(rchan, schan);
        default:
          throw new NotImplementedException("unexpected ChanDistributionType:" + dt);
      }
    }

    public static Func<IChanReceiver<T>, IChanSender<T>, ChanFactory<T, Unit>> FactoryFor<T>(ChanDistributionType dt) {
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

