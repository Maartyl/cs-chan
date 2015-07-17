using System.Threading.Tasks;
using System;

namespace Chan
{
  public class TaskCompletionCallback<T, TR> : TaskCompletionSource<T> {
    readonly Func<T,TR> callback;

    public TaskCompletionCallback(Func<T,TR> callback) {
      this.callback = callback;
    }

    public new TR SetResult(T result) {
      base.SetResult(result);
      return callback(result);
    }
  }
}

