// using System.Threading.Tasks;
using System;
using System.IO;

namespace Chan
{
  public class NetChanConfig {
    internal Stream In{ get; set; }

    internal Stream Out{ get; set; }

    protected NetChanConfig() {
      //default values
      InitialReceiveBufferSize = 1024;
      InitialSendBufferSize = 1024;
      PingDelayMs = 60 * 1000;
    }

    public int InitialReceiveBufferSize{ get; set; }

    public int InitialSendBufferSize{ get; set; }

    public int PingDelayMs { get; set; }

    public bool PropagateCloseFromSender{ get; set; }

    public bool PropagateCloseFromReceiver{ get; set; }
    //public int MembraneBufferSize { get; set; } //for world

    public static NetChanConfig<T> MakeDefault<T>() {
      var cfg = new NetChanConfig<T>();
      if (cfg.SerDes == null)
        throw new ArgumentException("Default config: {0} is not Serializable (requires SerDes)".Format(typeof(T)));
      return cfg;
    }
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
      get { return serDes ?? BinarySerDesForSerializable<T>.SerDes; }
      set { serDes = value; }
    }

    public NetChanConfig<T> Clone(Stream inS = null, Stream outS = null) {
      return new NetChanConfig<T> { 
        InitialReceiveBufferSize = InitialReceiveBufferSize,
        InitialSendBufferSize = InitialSendBufferSize,
        PingDelayMs = PingDelayMs,
        SerDes = serDes,
        PropagateCloseFromSender = PropagateCloseFromSender,
        PropagateCloseFromReceiver = PropagateCloseFromReceiver,
        In = inS ?? In,
        Out = outS ?? Out
      };     
    }
  }
}

