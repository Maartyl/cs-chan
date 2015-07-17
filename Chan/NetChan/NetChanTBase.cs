using System;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanTBase<T> : NetChanBase, IChanBase {
    //I present myself to world through membrane; using other side of it from the inside
    //protected readonly IChan<T> World;
    protected readonly ISerDes<T> SerDes;
    //wraps result of first call to CloseOnce
    readonly TaskCompletionSource<Task> closingTaskPromise = new TaskCompletionSource<Task>();

    protected NetChanTBase(NetChanConfig<T> cfg):base(cfg) {
      //World = cfg.InternalChannel;
      var sd = cfg.SerDes;
      if (sd == null)
        throw new ArgumentNullException("type(" + typeof(T) + ") is not serializable and requires valid SerDes`1");
      SerDes = sd;
    }

    public bool Closed { get { return closingTaskPromise.Task.IsCompleted; } }

    public virtual Task Close() {
      if (!Closed)
        lock (closingTaskPromise)
          if (!Closed) 
            closingTaskPromise.SetResult(CloseOnce());
      return AfterClosed();
    }

    ///only called once
    protected abstract Task CloseOnce();

    public async Task AfterClosed() {
      await await closingTaskPromise.Task;
    }
  }
}

