using System.Threading.Tasks;
using System;

namespace Chan
{
  public abstract class Nothing {
    private Nothing() {
    }
  }

  public class TaskCompletionSourceEmpty :TaskCompletionSource<Nothing> {
    public void SetCompleted() {
      if (!base.TrySetResult(null)) //not called as first set
        if (!base.Task.IsCompleted) //something else
          throw Task.Exception;
    }
  }
}

