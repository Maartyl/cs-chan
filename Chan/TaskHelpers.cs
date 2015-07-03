using System.Threading.Tasks;
using System;

namespace Channels
{
  public static class TaskHelpers {
    private static Task cancelledTask;

    public static Task CancelledTask { get { return cancelledTask; } }

    static TaskHelpers() {
      var tcs = new TaskCompletionSource<bool>();
      tcs.SetCanceled();
      cancelledTask = tcs.Task;
    }
  }
}

