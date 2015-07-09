using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanTBase<T> : NetChanBase {
    //I present myself to world through membrane; using other side of it from the inside
    protected readonly IChan<T> World;
    protected readonly ISerDes<T> SerDes;

    protected NetChanTBase(Stream netIn, Stream netOut, NetChanConfig<T> cfg):base(netIn, netOut, cfg) {
      World = cfg.Channel;
      var sd = cfg.SerDes;
      if (sd == null)
        throw new ArgumentNullException("type(" + typeof(T) + ") is not serializable and requires valid SerDes`1.");
      SerDes = sd;
    }
  }
}

