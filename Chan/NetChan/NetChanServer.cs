using System.Threading.Tasks;
using System;
using System.Net.Sockets;

namespace Chan
{
  //not necessarily connected end-points of tcp : just something that can open those
  public abstract class NetChanServer : IChanBase {
    TaskCollector collector = new TaskCollector();
    readonly InvokeOnceEmbeddable closing;

    protected NetChanServer() {
      closing = new InvokeOnceEmbeddable(CloseOnce);
    }
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

    public abstract void StartSender(TcpClient client, uint key);

    public abstract void StartReceiver(TcpClient client, uint key);

    internal void CollectServerConnectedTask(Task t) {
      //ex shouldn't close channel: only someone didn't connect... ?
      collector.Add(t);
    }
    #region IChanBase implementation
    public Task Close() {
      return closing.Invoke();
    }

    public Task AfterClosed() {
      return closing.AfterInvoked();
    }

    protected abstract Task CloseOnce();
    #endregion
  }

  public class NetChanServer<T> : NetChanServer {

    public NetChanServer(IChanFactory<Nothing> crossLocal, NetChanConfig cfg):base() {
    }
    #region implemented abstract members of NetChanServer
    public override void StartSender(TcpClient client, uint key) {
      throw new NotImplementedException();
    }

    public override void StartReceiver(TcpClient client, uint key) {
      throw new NotImplementedException();
    }
    #endregion
    #region implemented abstract members of NetChanServer
    protected override Task CloseOnce() {
      throw new NotImplementedException();
    }
    #endregion
  }
}

