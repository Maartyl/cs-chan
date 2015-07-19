using System;
using System.Threading.Tasks;

namespace Chan
{
  //idea: abstract variant of CloseOnce in channels - useful for other situations too
  // ... but it is kind of... imperfect...
  // : I don't want a class: wasting; and like this, can be instantiated incorrectly...
  //... it's still fairly cheap, though... (I think :- 1 delegate)
  internal struct InvokeOnceEmbeddable {
    readonly Func<Task> fn;
    readonly TaskCompletionSource<Task> invokingTaskPromise;

    /// 
    /// <param name="resultProvider">Result provider.</param>
    public InvokeOnceEmbeddable(Func<Task> invokeOnce) {
      invokingTaskPromise = new TaskCompletionSource<Task>();
      this.fn = invokeOnce;
    }

    public bool Invoked { get { return invokingTaskPromise.Task.IsCompleted; } }

    public Task Invoke() {
      if (!Invoked)
        lock (invokingTaskPromise)
          if (!Invoked) 
            invokingTaskPromise.SetResult(fn());
      return AfterInvoked();
    }

    public Task AfterInvoked() {
      return invokingTaskPromise.Task.Flatten();
    }
  }
}

