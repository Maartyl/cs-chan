using System.Threading.Tasks;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Collections.Generic;

namespace Chan
{
  //not necessarily connected end-points of tcp : just something that can open those
  public abstract class NetChanServer : IChanBase {
    readonly protected IChanFactory<Unit> localChan;
    readonly protected TaskCollector connectingExCollector = new TaskCollector();
    readonly protected TaskCollector runningExCollector = new TaskCollector();
    readonly InvokeOnceEmbeddable closing;
    volatile bool isClosed = false;

    protected NetChanServer(IChanFactory<Unit> localChan) {
      closing = new InvokeOnceEmbeddable(CloseOnce);
      this.localChan = localChan;
    }

    public bool IsClosed { get { return isClosed; } protected set { isClosed = value; } }

    public ChanDistributionType Type { get; protected set; }

    public string Name{ get; protected set; }

    ///does not include netIn, netOut (if does, will be ignored)
    public NetChanConfig Config { get; protected set; }

    public abstract void StartSenderCounterpart(TcpClient client, uint key);

    public abstract void StartReceiverCounterpart(TcpClient client, uint key);

    internal void CollectServerConnectedTask(Task t) {
      connectingExCollector.Add(t);
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
    readonly HashSet<NetChanSenderServer<T>> senders = new HashSet<NetChanSenderServer<T>>();
    readonly HashSet<NetChanReceiverServer<T>> receivers = new HashSet<NetChanReceiverServer<T>>();
    readonly NetChanConfig<T> config;
    readonly object collectionsLock = new object();

    public NetChanServer(IChanFactory<Unit> crossLocal, NetChanConfig<T> cfg,
                         string name, ChanDistributionType type) : base(crossLocal) {
      this.config = cfg;
      Name = name;
      Type = type;
    }

    async Task HandleNetChanServerClose(Task running, Task pipe, Action unregister) {
      var t = Task.WhenAll(running, pipe);
      try {
        await t;
      } finally {
        lock (collectionsLock)
          unregister();
      }
    }

    #region implemented abstract members of NetChanServer

    public override void StartSenderCounterpart(TcpClient client, uint key) {
      var s = client.GetStream();
      var cfg = config.Clone(s, s);
      var srv = new NetChanReceiverServer<T>(cfg);
      var running = srv.Start(key);
      lock (collectionsLock) {
        if (IsClosed) {
          Close();
          throw new InvalidOperationException("server closed");
        }
        receivers.Add(srv);
      }
      var pipe = srv.Pipe(localChan.GetSender<T>(), cfg.PropagateCloseFromSender);
      runningExCollector.Add(HandleNetChanServerClose(running, pipe, () => receivers.Remove(srv)));
    }

    public override void StartReceiverCounterpart(TcpClient client, uint key) {
      var s = client.GetStream();
      var cfg = config.Clone(s, s);
      var srv = new NetChanSenderServer<T>(cfg);
      var running = srv.Start(key);
      lock (collectionsLock) {
        if (IsClosed) {
          srv.Close();
          throw new InvalidOperationException("server closed");
        }
        senders.Add(srv);
      }
      var pipe = localChan.GetReceiver<T>().Pipe(srv, cfg.PropagateCloseFromReceiver);
      runningExCollector.Add(HandleNetChanServerClose(running, pipe, () => senders.Remove(srv)));
    }

    protected override Task CloseOnce() {
      IsClosed = true;
      runningExCollector.Close();
      connectingExCollector.Close();
      var sc = senders.Select(x => x.Close());
      var rc = receivers.Select(x => x.Close());
      var others = new Task[] { runningExCollector.Task, connectingExCollector.Task };
      return Task.WhenAll(sc.Concat(rc).Concat(others));
    }

    #endregion
  }
}

