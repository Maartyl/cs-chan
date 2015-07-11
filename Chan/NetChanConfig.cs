// using System.Threading.Tasks;
using System;
using System.IO;

namespace Chan
{
  public class NetChanConfig {
    internal Stream In{ get; set; }

    internal Stream Out{ get; set; }

    public int InitialReceiveBufferSize{ get; set; }

    public int InitialSendBufferSize{ get; set; }

    public int PingDelayMs { get; set; }
    //public int MembraneBufferSize { get; set; } //for world
  }

  public class NetChanConfig<T> : NetChanConfig {
    //    IChan<T> channel;
    //
    //    public IChan<T> InternalChannel {
    //      get { return channel = channel ?? new ChanAsync<T>();}
    //      set { channel = value;}
    //    }
    ISerDes<T> serDes;

    public ISerDes<T> SerDes {
      //no need to specify SerDes for serializable types
      get { return serDes ?? BinarySerDesForSerializable<T>.SerDes;}
      set { serDes = value;}
    }
  }
}

