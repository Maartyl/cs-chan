using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Chan
{
  ///allows access to registered channels
  /// - this class is not thread safe
  public class ChanStore : INetChanProvider {
    Dictionary<string, ChanBox> locals = new Dictionary<string, ChanBox>();
    ServiceHost netChanProviderHost;

    public ChanStore() {
      netChanProviderHost = new ServiceHost(this);
    }
    #region server start, stop
    public void StartServer(Uri address, Binding binding) {
      netChanProviderHost.AddServiceEndpoint(
        typeof(INetChanProvider), binding, address);
      netChanProviderHost.Open();
    }

    public void StartServer(int port, Binding binding) {
      StartServer(new Uri("http://0.0.0.0:" + port), binding);
    }

    public void StartServer(int port) {
      StartServer(port, new BasicHttpBinding());
    }

    public void StopServer() {
      netChanProviderHost.Close();
    }
    #endregion
    public Task CreateLocalChan<T>(string name, ChanDistributionType type = ChanDistributionType.FirstOnly) {
      if (locals.Keys.Contains(name))
        throw new ArgumentException("chan with given name already exists");
      var c = new ChanAsync<T>();
      locals.Add(name, new ChanBox(Chan.FactoryFor(type, c, c)));
      return c.AfterClosed();
    }

    public Task CreateNetChan<T>(string name, NetChanConfig cfg, ChanDistributionType type = ChanDistributionType.FirstOnly) {
      if (locals.Keys.Contains(name))
        throw new ArgumentException("chan with given name already exists");
      var dtf = Chan.FactoryFor<T>(type);
      var box = FromChanCrossPair(dtf, dtf, (l,r) => new ChanBox(l, new NetChanServer<T>(r, cfg)));
      locals.Add(name, box);
      return box.Server.AfterClosed();
    }
    //    public void RegisterReceiver<T>(Uri chanUri, IChanReceiver<T> chan) {
    //      ChanUriValidation(chanUri);
    //      if (chan == null)
    //        throw new ArgumentNullException("chan");
    //      if (chanUri.Authority != "")
    //        throw new ArgumentException("can only register local chans (is: {0})".Format(chanUri));
    //
    //      locals.Add(chanUri.AbsolutePath, chan);
    //    }
    static TR GetWrongTypeThrow<T, TR>(Type actualGenericType) {
      throw new ArgumentException("wrong chan type({0}); actual: {1}".Format(
        typeof(T), actualGenericType.Name));
    }

    IChanReceiver<T> GetLocalReceiver<T>(string chanName) {
      ChanBox box; //null if not exists; wrong type: exception
      return !locals.TryGetValue(chanName, out box) ? null 
        : box.Chan.GetReceiver<T>() ?? GetWrongTypeThrow<T, IChanReceiver<T>>(box.Chan.GenericType);
    }

    IChanSender<T> GetLocalSender<T>(string chanName) {
      ChanBox box; //null unless exists; wrong type: exception
      return !locals.TryGetValue(chanName, out box) ? null 
        : box.Chan.GetSender<T>() ?? GetWrongTypeThrow<T, IChanSender<T>>(box.Chan.GenericType);
    }

    public IChanReceiver<T> GetReceiver<T>(Uri chanUri) {
      ChanUriValidation(chanUri);

      if (chanUri.Authority == "") 
        return GetLocalReceiver<T>(chanUri.AbsolutePath);
      else {
        //remote
        //TODO: how to store connections... - reuse for sure.
        throw new NotImplementedException();
      }
    }

    void ChanUriValidation(Uri chanUri) {
      if (chanUri == null)
        throw new ArgumentNullException("chanUri");
      if (chanUri.Scheme != "chan") 
        throw new ArgumentException("requires uri with chan scheme (is: {0})".Format(chanUri), "chanUri");
    }
    #region INetChanProvider implementation
    Task<TcpClient> NetChanListen(out int port) { 
      RemoteEndpointMessageProperty endpointP = //address of sender (who requests chan)
        OperationContext.Current.IncomingMessageProperties
          [RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
      var addr = endpointP == null ? IPAddress.Any : IPAddress.Parse(endpointP.Address);
      var listener = new TcpListener(addr, 0);//0 == some not used port
      listener.Start(); //initializes socket and assignes port
      port = ((IPEndPoint) listener.LocalEndpoint).Port;
      return TcpListenerAcceptOne(listener);
    }

    async Task<TcpClient> TcpListenerAcceptOne(TcpListener listener) {
      var c = await listener.AcceptTcpClientAsync();
      listener.Stop();
      return c;
    }

    async Task OpenNetChanServer(Task<TcpClient> client, uint key, Action<TcpClient, uint> register) {
      register(await client, key);
    }

    uint RandomKey() {
      return unchecked((uint) new Random().Next());
    }

    NetChanConnectionInfo INetChanProvider.RequestSender(Uri chanName) {
      throw new NotImplementedException();
    }

    NetChanConnectionInfo INetChanProvider.RequestReceiver(Uri chanName) {
      throw new NotImplementedException();
    }
    #endregion
    protected class ChanBox {
      public IChanFactory<Nothing> Chan{ get; private set; }

      public NetChanServer Server{ get; private set; }

      public ChanBox(IChanFactory<Nothing> local, NetChanServer server) {
        Chan = local;
        Server = server;
      }

      public ChanBox(IChanFactory<Nothing> local):this(local, null) {
      }

      public bool IsNetChan{ get { return Server != null; } }
    }

    private T FromChanCrossPair<T, TM, TL, TR>(Func<IChanReceiver<TM>, IChanSender<TM>, TL> fl,
                                               Func<IChanReceiver<TM>, IChanSender<TM>, TR> fr,
                                               Func<TL, TR, T> f) {
      var c1 = new ChanAsync<TM>();
      var c2 = new ChanAsync<TM>();
      return f(fl(c1, c2), fr(c2, c1));
      //(defn from-chan-cross-pair [fl fr f] (let [c1 (ChanAsync.) c2 (ChanAsync.)] (f (fl c1 c2) (fr c2 c1))))
      // #let a 5 (+ 7 a)
    }
  }

  public enum ChanDistributionType {
    Broadcast,
    FirstOnly
  }
}

