using System;
using System.Threading;

namespace Chan
{
  ///synchronizes 2 threads: producer (calls Start) and consumer (calls Deliver)
  /// - consumer needs to access DeliverBarrier through passed action (i.e. it saves it somewhere both can access)
  /// - Producer is blocked until Deliver is called. (Deliver can be called only once)
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

    public static void Start(T data, Action<DeliverBarrier<T>> register) { 
      var db = new DeliverBarrier<T>(data);
      register.Invoke(db);
      db.mre.Wait();
      db.mre.Dispose();
    }
  }

  public static class DeliverBarrier {
    public static void Start<T>(T data, Action<DeliverBarrier<T>> register) { 
      DeliverBarrier<T>.Start(data, register);
    }
  }
}

