using System;
using System.Threading.Tasks;

namespace Chan
{
  /// like DeliverBarrier but async
  public class DeliverAsync<T> {
    readonly T data;
    readonly TaskCompletionSource<Task> t = new TaskCompletionSource<Task>();

    DeliverAsync(T data) {
      this.data = data;
    }

    public T Deliver(Func<T, Task> sendCallback) {
      t.SetResult(sendCallback(data));
      return data;
    }

    public Task Task { get { return t.Task.Flatten(); } }

    public static Task Start(T data, Action<DeliverAsync<T>> register) { 
      var da = Create(data);
      register.Invoke(da);
      return da.Task;
    }

    public static DeliverAsync<T> Create(T data) {
      return new DeliverAsync<T>(data);
    }
  }

  public static class DeliverAsync {
    public static Task Start<T>(T data, Action<DeliverAsync<T>> register) { 
      return DeliverAsync<T>.Start(data, register);
    }

    public static DeliverAsync<T> Create<T>(T data) {
      return DeliverAsync<T>.Create(data);
    }
  }
}

