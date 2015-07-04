using System.Threading.Tasks;
using System;

namespace Chan
{
  public abstract class Nothing {
    private Nothing() {
    }
  }

  public class TaskCompletionSuorceEmpty :TaskCompletionSource<Nothing> {
    public void SetCompleted() {
      base.SetResult(null);
    }
  }
}

