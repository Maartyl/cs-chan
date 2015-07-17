using System.Threading.Tasks;
using System;

namespace Chan
{
  internal static class TaskHelpers {
    private static Task cancelledTask;

    public static Task CancelledTask { get { return cancelledTask; } }

    static TaskHelpers() {
      var tcs = new TaskCompletionSource<bool>();
      tcs.SetCanceled();
      cancelledTask = tcs.Task;
    }

    public static Task Flatten(this Task<Task> t) {
      return t.Bind(x => x);
    }

    public static Task<T> Flatten<T>(this Task<Task<T>> t) {
      return t.Bind(x => x);
    }

    public static async Task Bind<T>(this Task<T> t, Func<T, Task> f) {
      await f(await t);
    }

    /// >>=
    public static async Task<TR> Bind<T, TR>(this Task<T> t, Func<T, Task<TR>> f) {
      return await f(await t);
    }

    public static async Task Bind(this Task t, Func<Task> f) {
      await t;
      await f();
    }

    public static async Task<TR> Bind<TR>(this Task t, Func<Task<TR>> f) {
      await t;
      return await f();
    }
  }
}

