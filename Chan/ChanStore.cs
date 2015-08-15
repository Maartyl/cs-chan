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
  [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
  public class ChanStore : INetChanProvider {
    readonly Dictionary<string, ChanBox> locals = new Dictionary<string, ChanBox>();
    readonly Dictionary<Type, NetChanClientCacheReceiver> clientReceivers = new Dictionary<Type, NetChanClientCacheReceiver>();
    readonly Dictionary<Type, NetChanClientCacheSender> clientSenders = new Dictionary<Type, NetChanClientCacheSender>();
    readonly Dictionary<Uri, Binding> clientBindingsSender = new Dictionary<Uri, Binding>();
    readonly Dictionary<Uri, Binding> clientBindingsReceiver = new Dictionary<Uri, Binding>();
    readonly ServiceHost netChanProviderHost;
    int connectingServerTimeout = 30 * 1000;
    //int connectionClientTimeout = 40 * 1000;

    public ChanStore() {
      netChanProviderHost = new ServiceHost(this/*what implements interface*/);
    }

    Binding defaultBinding = new BasicHttpBinding();

    public Binding DefaultBinding {
      get { return defaultBinding; }
      set {
        if (value != null)
          defaultBinding = value;
      }
    }

    public int ConnectingServerTimeout {
      get { return connectingServerTimeout; }
      set { connectingServerTimeout = value; }
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
      StartServer(port, defaultBinding);
    }

    public void StopServer() {
      netChanProviderHost.Close();
    }

    #endregion

    #region creation

    public Task CreateLocalChan<T>(string name, ChanDistributionType type = ChanDistributionType.FirstOnly) {
      name = normalizeChanName(name);
      if (locals.Keys.Contains(name))
        throw new ArgumentException("chan with given name already exists");
      var c = new ChanAsync<T>();
      locals.Add(name, new ChanBox(Chan.FactoryFor(type, c, c)));
      return c.AfterClosed();
    }

    public Task CreateNetChan<T>(string name, NetChanConfig<T> cfg, ChanDistributionType type = ChanDistributionType.FirstOnly) {
      name = normalizeChanName(name);
      if (locals.Keys.Contains(name))
        throw new ArgumentException("chan with given name already exists");
      var dtf = Chan.FactoryFor<T>(type);
      var box = Chan.FromChanCrossPair(dtf, dtf, (l, r) => new ChanBox(l, new NetChanServer<T>(r, cfg, name, type)));
      locals.Add(name, box);
      return box.Server.AfterClosed();
    }

    public Task PrepareClientSenderForType<T>(NetChanConfig<T> cfg) {
      lock (clientSenders) {
        if (clientSenders.ContainsKey(typeof(T)))
          throw new ArgumentException("Client configuration for type is present.");
        return (clientSenders[typeof(T)] = new NetChanClientCacheSender<T>(cfg)).CollectedExceptions; 
      }
    }

    public Task PrepareClientReceiverForType<T>(NetChanConfig<T> cfg) {
      lock (clientReceivers) {
        if (clientReceivers.ContainsKey(typeof(T)))
          throw new ArgumentException("Client configuration for type is present.");
        return (clientReceivers[typeof(T)] = new NetChanClientCacheReceiver<T>(cfg)).CollectedExceptions; 
      }
    }

    public void RegisterClientSenderBinding(Uri chan, Binding binding) {
      clientBindingsSender[chan] = binding;
    }

    public void RegisterClientReceiverBinding(Uri chan, Binding binding) {
      clientBindingsReceiver[chan] = binding;
    }

    #endregion

    #region get local

    static TR GetWrongTypeThrow<T, TR>(Type actualGenericType) { 
      throw new ArgumentException("wrong chan type({0}); actual: {1}".Format(
        typeof(T), actualGenericType.Name));
    }

    IChanReceiver<T> GetLocalReceiver<T>(string chanName) {
      return GetLocalGeneric<T,IChanReceiver<T>>(chanName, Exts.GetReceiver<T>);
    }

    IChanSender<T> GetLocalSender<T>(string chanName) {
      return GetLocalGeneric<T,IChanSender<T>>(chanName, Exts.GetSender<T>);
    }

    TC GetLocalGeneric<T, TC>(string chanName, Func<IChanFactory<Unit>, TC> getChan) where TC : class {
      ChanBox box; //null unless exists; wrong type: exception
      return !locals.TryGetValue(chanName, out box) ? null 
        : getChan(box.Chan) ?? GetWrongTypeThrow<T, TC>(box.Chan.GenericType);
    }

    #endregion

    public IChanReceiver<T> GetReceiver<T>(Uri chanUri, Binding binding = null) {
      ChanUriValidation(chanUri);

      return chanUri.Authority == "" 
        ? GetLocalReceiver<T>(chanUri.AbsolutePath) 
          : GetClient<T, IChanReceiverFactory<Unit>, NetChanClientCacheReceiver>(
        chanUri, clientBindingsReceiver, binding, clientReceivers).GetReceiver<T>();
    }

    public IChanSender<T> GetSender<T>(Uri chanUri, Binding binding = null) {
      ChanUriValidation(chanUri);

      return chanUri.Authority == "" 
        ? GetLocalSender<T>(chanUri.AbsolutePath) 
          : GetClient<T, IChanSenderFactory<Unit>, NetChanClientCacheSender>(
        chanUri, clientBindingsSender, binding, clientSenders).GetSender<T>();
    }

    T GetClient<TMsg, T, TC>(Uri chanUri, IDictionary<Uri, Binding> dfltB,
                             Binding binding, IDictionary<Type, TC> cache) 
      where TC : NetChanClientCache<T> {
      Binding bindDflt;
      dfltB.TryGetValue(chanUri, out bindDflt); //null is fine: would blow, but potentially unnecessary
      TC factoryCache;
      if (cache.TryGetValue(typeof(TMsg), out factoryCache)) //cache: type -> clientCache -> factory -> ret
        return factoryCache.Get(chanUri, binding ?? bindDflt ?? DefaultBinding);
      throw new InvalidOperationException("Client type not initialized");
    }

    //TODO: free snder/receiver
    //

    static void ChanUriValidation(Uri chanUri) {
      if (chanUri == null)
        throw new ArgumentNullException("chanUri");
      if (chanUri.Scheme != "chan")
        throw new ArgumentException("requires uri with chan scheme (is: {0})".Format(chanUri), "chanUri");
    }

    static string normalizeChanName(string name) {
      return new Uri("chan:" + name).AbsolutePath.TrimStart('/');
    }

    #region INetChanProvider implementation

    Task<TcpClient> NetChanListen(out int port) { 
      RemoteEndpointMessageProperty endpointP = //address of sender (who requests chan)
        OperationContext.Current.IncomingMessageProperties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
      var addr = endpointP == null ? IPAddress.Any : IPAddress.Parse(endpointP.Address);
      var listener = new TcpListener(addr, 0);//0 == some not used port
      listener.Start(); //initializes socket and assignes port
      port = ((IPEndPoint) listener.LocalEndpoint).Port;
      return TcpListenerAcceptOne(listener);
    }

    async Task<TcpClient> TcpListenerAcceptOne(TcpListener listener) {
      var cT = listener.AcceptTcpClientAsync();
      var timeout = Task.Delay(connectingServerTimeout);
      if (Task.WhenAny(cT, timeout) == timeout)
        throw new TimeoutException("No client connected to listener. (timeout: {0}ms)".Format(connectingServerTimeout));
      var c = await cT;
      listener.Stop();
      return c;
    }

    static async Task OpenNetChanServer(Task<TcpClient> client, uint key, Action<TcpClient, uint> register) {
      register(await client, key);
    }

    static uint RandomKey() {
      return unchecked((uint) new Random().Next());
    }

    NetChanConnectionInfo RequestFinish(Action<TcpClient, uint> register, NetChanServer srv) { 
      //register is either (server) .StartSender or .StartReceiver
      int port; //set by listener to some open port
      var key = RandomKey();
      srv.CollectServerConnectedTask(//completes when client connected and NetChan created
        OpenNetChanServer(NetChanListen(out port), key, register));

      return new NetChanConnectionInfo { 
        Key = key, Port = port, Type = srv.Type
      };
    }

    NetChanServer RequestStart(Uri chanName) {
      //determine - this part is mostly checks; starting server is in RequestFinish
      //checks... - thinking back: why uri? = oh, for potential future args, ok
      // - it has to be a local uri
      ChanUriValidation(chanName);
      if (chanName.Authority != "")
        throw new ArgumentException("Only local uri can be requested. (is: {0})".Format(chanName));
      ChanBox box;
      var name = normalizeChanName(chanName.AbsolutePath);
      if (!locals.TryGetValue(name, out box))
        throw new KeyNotFoundException("No chan registered under: {0}".Format(name as object));
      if (!box.IsNetChan)
        throw new ArgumentException("Requested chan is only locally accessible. ({0})"
                                    .Format(name as object));
      var s = box.Server;
      if (s.IsClosed)
        throw new InvalidOperationException("Server closed.");

      return s;
    }

    NetChanConnectionInfo Request(Uri chanName, Func<NetChanServer,Action<TcpClient, uint>> starter) {
      try {
        var s = RequestStart(chanName);
        return RequestFinish(starter(s), s);
      } catch (Exception ex) {
        return new NetChanConnectionInfo { 
          ErrorCode = ex.HResult,
          ErrorMessage = ex.Message,
          ErrorType = ex.GetType().FullName
        };
      }
    }

    NetChanConnectionInfo INetChanProvider.RequestSender(Uri chanName) {
      return Request(chanName, x => x.StartSenderCounterpart);
    }

    NetChanConnectionInfo INetChanProvider.RequestReceiver(Uri chanName) {
      return Request(chanName, x => x.StartReceiverCounterpart);
    }

    #endregion

    protected sealed class ChanBox {
      public IChanFactory<Unit> Chan{ get; private set; }

      public NetChanServer Server{ get; private set; }

      public ChanBox(IChanFactory<Unit> local, NetChanServer server) {
        Chan = local;
        Server = server;
      }

      public ChanBox(IChanFactory<Unit> local) : this(local, null) {
      }

      public bool IsNetChan{ get { return Server != null; } }
    }
  }

  public enum ChanDistributionType {
    Broadcast,
    FirstOnly
  }
}

