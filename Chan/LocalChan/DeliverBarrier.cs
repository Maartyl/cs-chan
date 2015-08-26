using System;
using System.Threading;

namespace Chan
{
  ///synchronizes 2 threads: producer (calls Start) and consumer (calls Deliver)
  /// - consumer needs to access DeliverBarrier through passed action (i.e. it saves it somewhere both can access)
  /// - Producer is blocked until Deliver is called. (Deliver can be called only once)
  /// (original variant for ChanBlocking: replaced with ChanAsync)
  public class DeliverBarrier<T> {
    readonly T data;
    readonly ManualResetEventSlim mre = new ManualResetEventSlim();

    DeliverBarrier(T data) {
      this.data = data;
    }

    public T Deliver() {
      mre.Set();
      return data;
    }

    public void WaitAndDispose() {
      mre.Wait();
      mre.Dispose();
    }

    public static void Wait(T data, Action<DeliverBarrier<T>> register) { 
      var db = Create(data);
      register.Invoke(db);
      db.WaitAndDispose();
    }

    public static DeliverBarrier<T> Create(T data) {
      return new DeliverBarrier<T>(data);
    }
  }

  public static class DeliverBarrier {
    public static void Wait<T>(T data, Action<DeliverBarrier<T>> register) { 
      DeliverBarrier<T>.Wait(data, register);
    }

    public static DeliverBarrier<T> Create<T>(T data) {
      return DeliverBarrier<T>.Create(data);
    }
  }
}

