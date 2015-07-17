using System.Threading.Tasks;
using System;
using System.Net.Sockets;

namespace Chan
{
  //not necessarily connected end-points of tcp : just something that can open those
  public abstract class NetChanServer : IChanBase {
    //has: interface to server
    // - interface to "outside" (local)
    // - chan name (absolute path of local uri)
    // - ChanDistributionType
    // interfaces are methods that return chans:
    // - It returns the same iff type == firstOnly; a different one (each fed from tee) for BC
    public ChanDistributionType Type { get; protected set; }

    public string Name{ get; protected set; }

    ///does not include netIn, netOut
    public NetChanConfig Config { get; protected set; }

    public abstract void Start(TcpClient client, uint key);
    #region IChanBase implementation
    public Task Close() {
      throw new NotImplementedException();
    }

    public Task AfterClosed() {
      throw new NotImplementedException();
    }
    #endregion
  }

  public class NetChanServer<T> : NetChanServer {
    public NetChanServer(IChanFactory<Nothing> crossLocal, NetChanConfig cfg) {
    }
    #region implemented abstract members of NetChanServer
    public override void Start(TcpClient client, uint key) {
      throw new NotImplementedException();
    }
    #endregion
  }
}

