using System.Threading.Tasks;
using System;

namespace Chan
{
  public class TaskCompletionSourceEmpty :TaskCompletionSource<Unit> {
    public void SetCompleted() {
      if (!base.TrySetResult(null)) //not called as first set
        if (!base.Task.IsCompleted) //something else
          throw Task.Exception;
    }
  }
}

