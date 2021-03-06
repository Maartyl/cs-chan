using System.Threading.Tasks;
using System;

namespace Chan
{
  ///only handles first exception
  public class ExceptionDrain {
    readonly TaskCompletionSourceEmpty p = new TaskCompletionSourceEmpty();

    public Task Task{ get { return p.Task; } }

    ///first task that completes with error will set the Exception in this.Task
    ///will ignore RanToCompletion in all cases
    public void Consume(Task task, bool registerCancel = false) {
      var t2 = task.ContinueWith(t => p.TrySetException(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
      if (registerCancel)//OK ignored on purpose
        t2.ContinueWith(_ => p.TrySetCanceled(), TaskContinuationOptions.OnlyOnCanceled);
    }

    public void EndOk() {
      p.TrySetCompleted();
    }
  }
}

