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
    volatile int isClosed;

    public InvokeOnceEmbeddable(Func<Task> invokeOnce) {
      isClosed = 0;
      invokingTaskPromise = new TaskCompletionSource<Task>();
      fn = invokeOnce;
    }

    public bool Invoked { get { return isClosed != 0; } }

    /// retVal == this call changed to closed
    bool SetInvoked() {
      #pragma warning disable 0420 //volatile ref
      return 0 == Interlocked.Exchange(ref isClosed, 1);
      #pragma warning restore 0420
    }

    /// <summary>
    /// Invokes provided function if called for the first time.
    /// </summary>
    /// <returns>AfterInvoked()</returns>
    public Task Invoke() {
      if (!Invoked && SetInvoked())
        invokingTaskPromise.TrySetResult(fn());
      return AfterInvoked;
    }

    /// <summary>
    /// Task equivalent to the one returned from provided delegate.
    /// </summary>
    /// <returns>Task returned from first call to provided delegate. - Not necessarily the same object.</returns>
    public Task AfterInvoked { get { return invokingTaskPromise.Task.Flatten(); } }
  }
}

