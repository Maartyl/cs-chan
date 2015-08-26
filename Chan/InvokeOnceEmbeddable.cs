using System;
using System.Threading.Tasks;
using System.Threading;

namespace Chan
{
  //idea: abstract variant of CloseOnce in channels - useful for other situations too
  // ... but it is kind of... imperfect...
  // : I don't want a class: wasting; and like this, can be instantiated incorrectly...
  //... it's still fairly cheap, though... (I think :- 1 instance delegate)
  internal struct InvokeOnceEmbeddable {
    readonly Func<Task> fn;
    readonly TaskCompletionSource<Task> invokingTaskPromise;
    //cannot use closingTask.Promise..IsCompleted: set AFTER running fn
    // - I have to set right away
    ///bool but need Interlocked (0=false; otherwise true)
    int isClosed;

    public InvokeOnceEmbeddable(Func<Task> invokeOnce) {
      isClosed = 0;
      invokingTaskPromise = new TaskCompletionSource<Task>();
      this.fn = invokeOnce;
    }

    public bool Invoked { get { return isClosed != 0; } }

    /// retVal == this call changed to closed
    bool SetInvoked() {
      return 0 == Interlocked.Exchange(ref isClosed, 1);
    }

    public Task Invoke() {
      if (!Invoked && SetInvoked())
        invokingTaskPromise.SetResult(fn());
      return AfterInvoked();
    }

    public Task AfterInvoked() {
      return invokingTaskPromise.Task.Flatten();
    }
  }
}

