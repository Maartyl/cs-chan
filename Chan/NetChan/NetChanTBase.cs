using System;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanTBase<T> : NetChanBase, IChanBase {
    //I present myself to world through membrane; using other side of it from the inside
    //different for sender and receiver
    //protected readonly IChan<T> World;
    protected readonly ISerDes<T> SerDes;
    //wraps result of first call to CloseOnce
    readonly InvokeOnceEmbeddable closing;

    protected NetChanTBase(NetChanConfig<T> cfg) : base(cfg) {
      //World = cfg.InternalChannel;
      var sd = cfg.SerDes;
      if (sd == null)
        throw new ArgumentNullException("type(" + typeof(T) + ") is not serializable and requires valid SerDes`1");
      SerDes = sd;
      closing = new InvokeOnceEmbeddable(CloseOnce);
    }

    public bool Closed { get { return closing.Invoked; } }

    public virtual Task Close() {
      return closing.Invoke();
    }

    ///only called once
    protected abstract Task CloseOnce();

    public Task AfterClosed() {
      return closing.AfterInvoked();
    }
  }
}

