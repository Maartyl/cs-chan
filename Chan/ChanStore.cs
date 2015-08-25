using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel.Description;

namespace Chan
{
  ///allows access to registered channels
  /// - this class is not thread safe
  [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)/*this allows to specify this as handler*/]
  public class ChanStore : INetChanProvider {
    readonly Dictionary<string, ChanBox> locals = new Dictionary<string, ChanBox>();
    readonly Dictionary<Type, NetChanClientCacheReceiver> clientReceivers = new Dictionary<Type, NetChanClientCacheReceiver>();
    readonly Dictionary<Type, NetChanClientCacheSender> clientSenders = new Dictionary<Type, NetChanClientCacheSender>();
    //default bindings per uri
    readonly Dictionary<Uri, Binding> clientBindingsSender = new Dictionary<Uri, Binding>();
    readonly Dictionary<Uri, Binding> clientBindingsReceiver = new Dictionary<Uri, Binding>();
    //invoked from .Free
    readonly Dictionary<IChanBase, Func<bool>> freeChans = new Dictionary<IChanBase, Func<bool>>();
    //WCF service used to open NetChans
    // - Closing it does not NetChans opened through it (are their own TCP connections)
    readonly ServiceHost netChanProviderHost;
    int connectingServerTimeout = 30 * 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="Chan.ChanStore"/> class.
    /// Wsdl is at :[wsdlPort]/ChanStore if specified
    /// </summary>
    /// <param name="wsdlPort">default: -1 == no wsdl</param>
    public ChanStore(int wsdlPort) {
      LimitClientIP = true;
      netChanProviderHost = new ServiceHost(this/*what implements interface*/, wsdlPort > 0 ? new[] {
        new Uri("http://" + Environment.MachineName + ":" + wsdlPort + "/ChanStore")
      } : new Uri[0]);
      if (wsdlPort > 0)
        netChanProviderHost.Description.Behaviors.Add(new ServiceMetadataBehavior{ HttpGetEnabled = true });
    }

    public ChanStore() : this(-1) {
      
    }

    Binding defaultBinding = new BasicHttpBinding();

    public Binding DefaultBinding {
      get { return defaultBinding; }
      set {
        if (value != null) defaultBinding = value;
      }
    }

    public int ConnectingServerTimeout {
      get { return connectingServerTimeout; }
      set { connectingServerTimeout = value; }
    }

    #region server start, stop

    ///Opens underlying WCF service for accessing net-chans remotely.
    public void StartServer(Uri address, Binding binding) {
      if (binding == null) throw new ArgumentNullException("binding");
      if (address == null) throw new ArgumentNullException("address");

      netChanProviderHost.AddServiceEndpoint(
        typeof(INetChanProvider), binding, address);
      netChanProviderHost.Open();
    }

    ///Opens underlying WCF service for accessing net-chans remotely.
    /// Uses: http://0.0.0.0
    public void StartServer(int port, Binding binding) {
      StartServer(new Uri("http://0.0.0.0:" + port), binding);
    }

    ///Opens underlying WCF service for accessing net-chans remotely.
    /// Uses DefaultBinding (SimpleHttp by default)
    public void StartServer(int port) {
      StartServer(port, defaultBinding);
    }

    ///Closes underlying WCF service for accessing net-chans remotely.
    public void StopServer() {
      netChanProviderHost.Close();
    }

    #endregion

    #region creation

    /// <summary>
    /// Creates a local chan that can be accessed only locally through Uri without Authority.
    /// </summary>
    /// <returns>Task collecting exceptions through creation, outside changes, ...</returns>
    /// <param name="name">Absolute Path of Uri while accessing. Leading '/' is ignored.
    ///  If specifies anything after path (query/fragment), those are ignored. Must be valid Uri path.</param>
    /// <param name="type">DistributionType</param>
    /// <typeparam name="T">Type of messages sent through this chan.</typeparam>
    public Task CreateLocalChan<T>(string name, ChanDistributionType type = ChanDistributionType.FirstOnly) {
      if (name == null) throw new ArgumentNullException("name");

      name = normalizeChanName(name);
      lock (locals) //I'm aware of race: not thread safe class
        if (locals.Keys.Contains(name)) throw new ArgumentException("chan with given name already exists");
      var c = new ChanAsync<T>();
      lock (locals)
        locals.Add(name, new ChanBox(Chan.FactoryFor(type, c, c)));
      return c.AfterClosed();
    }

    /// <summary>
    /// Creates a network-accessible chan that can be accessed through Uri with Authority. 
    /// Authority has to describe 'this' computer and port on which is ChanStore started.
    /// </summary>
    /// <returns>Task collecting exceptions through creation, outside changes, ...</returns>
    /// <param name="name">Absolute Path of Uri while accessing. Leading '/' is ignored.
    ///  If specifies anything after path (query/fragment), those are ignored. Must be valid Uri path.</param>
    /// /// <param name="cfg">Configuration file, specifing defaults and SerDes to serialize messages.
    ///  It is recommended to use NetChanConfig.Default (and possibly modify it).</param>
    /// <param name="type">DistributionType</param>
    /// <typeparam name="T">Type of messages sent through this chan. If type is not serializable, custom ISerDes must be provided.</typeparam>
    public Task CreateNetChan<T>(string name, NetChanConfig<T> cfg, ChanDistributionType type = ChanDistributionType.FirstOnly) {
      if (name == null) throw new ArgumentNullException("name");
      cfgNullCheck(cfg);
      
      name = normalizeChanName(name);
      lock (locals) //I'm aware of race: not thread safe class
        if (locals.Keys.Contains(name)) throw new ArgumentException("chan with given name already exists");
      var dtf = Chan.FactoryFor<T>(type);
      var box = Chan.FromChanCrossPair(dtf, dtf, (l, r) => new ChanBox(l, new NetChanServer<T>(r, cfg, name, type)));
      lock (locals)
        locals.Add(name, box);
      return box.Server.AfterClosed();
    }

    /// <summary>
    /// All sender-chan clients will use provided config when created in GetAsync.
    /// Calling this before GetAsync for every type is necessary.
    /// </summary>
    /// <returns>Task which collects exceptions in client.</returns>
    /// <param name="cfg">configuration for client</param>
    /// <typeparam name="T">chan message type</typeparam>
    public Task PrepareClientSenderForType<T>(NetChanConfig<T> cfg) {
      return PrepareClientForType<T, NetChanClientCacheSender, IChanSenderFactory<Unit>>
        (cfg, c => new NetChanClientCacheSender<T>(c), clientSenders);
    }

    /// <summary>
    /// All receiver-chan clients will use provided config when created in GetAsync.
    /// Calling this before GetAsync for every type is necessary.
    /// </summary>
    /// <returns>Task which collects exceptions in client.</returns>
    /// <param name="cfg">configuration for client</param>
    /// <typeparam name="T">chan message type</typeparam>
    public Task PrepareClientReceiverForType<T>(NetChanConfig<T> cfg) {
      return PrepareClientForType<T, NetChanClientCacheReceiver, IChanReceiverFactory<Unit>>
        (cfg, c => new NetChanClientCacheReceiver<T>(c), clientReceivers);
    }

    static Task PrepareClientForType<T, TC, TT>(NetChanConfig<T> cfg, Func<NetChanConfig<T>, TC> builder, Dictionary<Type,TC> clients)
      where TC : NetChanClientCache<TT> {
      cfgNullCheck(cfg);

      lock (clients)
        if (clients.ContainsKey(typeof(T)))
          throw new ArgumentException("Client configuration for type is present.");
        else return (clients[typeof(T)] = builder(cfg)).CollectedExceptions; 
    }

    static void cfgNullCheck<T>(NetChanConfig<T> cfg) {
      if (cfg == null) throw new ArgumentNullException("cfg");
      if (cfg.SerDes == null)
        throw new ArgumentNullException("" + "cfg.SerDes", "Not serializable types must provide custom ISerDes.");
    }

    /// <summary>
    /// Provide default binding for given Uri.
    /// Uri without authority will be ignored as it has to be Uri for accesing remote chans.
    /// This binding will be used, if null provided in GetAsync, instead of DefaultBinding.
    /// </summary>
    public void RegisterClientSenderBinding(Uri chan, Binding binding) {
      RegisterClientBinding(chan, binding, clientBindingsSender);
    }

    /// <summary>
    /// Provide default binding for given Uri.
    /// Uri without authority will be ignored as it has to be Uri for accesing remote chans.
    /// This binding will be used, if null provided in GetAsync, instead of DefaultBinding.
    /// </summary>
    public void RegisterClientReceiverBinding(Uri chan, Binding binding) {
      RegisterClientBinding(chan, binding, clientBindingsReceiver);
    }

    static void RegisterClientBinding(Uri chan, Binding binding, Dictionary<Uri, Binding> defaults) {
      ChanUriValidation(chan);
      if (!string.IsNullOrEmpty(chan.Authority))
        lock (defaults)
          defaults[chan.Normalize()] = binding;
    }

    #endregion

    #region get local

    static TR GetWrongTypeThrow<T, TR>(Type actualGenericType) {
      throw new ArgumentException("wrong chan type({0}); actual: {1}".Format(
        typeof(T), actualGenericType.Name));
    }

    IChanReceiver<T> GetLocalReceiver<T>(string chanName) { 
      return GetLocalGeneric<T,IChanReceiver<T>>(chanName, GetReceiverAndRememberToFree<T>);
    }

    IChanSender<T> GetLocalSender<T>(string chanName) {
      return GetLocalGeneric<T,IChanSender<T>>(chanName, GetSenderAndRememberToFree<T>);
    }

    TC GetLocalGeneric<T, TC>(string chanName, Func<IChanFactory<Unit>, TC> getChan) where TC : class {
      ChanBox box; //null unless exists; wrong type: exception
      return !locals.TryGetValue(chanName, out box) ? null 
        : getChan(box.Chan) ?? GetWrongTypeThrow<T, TC>(box.Chan.GenericType);
    }

    #endregion

    #region free and get from factory

    //saves in freeChans map: called from Free
    //thanks to this, I don't have to look for it later...
    void RememberToFree(IChanFactoryBase f, IChanBase chan) {
      lock (freeChans)
        freeChans[chan] = () => {
          var ret = f.Free(chan);
          //if (ret) - FactoryWrap returns false always (which is not very good, but.... - change return type...?)
          lock (freeChans)
            freeChans.Remove(chan); //remove from non-freed (if got freed)
          return ret; //free the chan;
        };
    }

    IChanSender<T> GetSenderAndRememberToFree<T>(IChanSenderFactory<Unit> f) { 
      var chan = f.GetSender<T>();
      RememberToFree(f, chan);
      return chan;
    }

    IChanReceiver<T> GetReceiverAndRememberToFree<T>(IChanReceiverFactory<Unit> f) { 
      var chan = f.GetReceiver<T>();
      RememberToFree(f, chan);
      return chan;
    }

    #endregion

    #region get chan

    ///variant ot be used when accessing local chans
    public IChanReceiver<T> GetReceiver<T>(Uri chanUri) {
      return GetReceiverAsync<T>(chanUri).Result;
    }

    ///variant to be used when accessing local chans
    public IChanSender<T> GetSender<T>(Uri chanUri) {
      return GetSenderAsync<T>(chanUri).Result;
    }

    /// <summary>
    /// Gets chan receiver from store.
    /// </summary>
    /// <returns>receiver end of chan</returns>
    /// <param name="chanUri">uri representing chan: authority will be used for ChanStoreServer lookup 
    /// and path for determining chan.
    /// No authority means local lookup from side of server.
    /// Localhost authority means local lookup from side of client.</param>
    /// <param name="binding">if needs special binding; ignored if cached</param>
    /// <typeparam name="T">type of messagse in chan</typeparam>
    public async Task<IChanReceiver<T>> GetReceiverAsync<T>(Uri chanUri, Binding binding = null) {
      ChanUriValidation(chanUri);

      return string.IsNullOrEmpty(chanUri.Authority)
        ? GetLocalReceiver<T>(chanUri.AbsolutePath) 
          : GetReceiverAndRememberToFree<T>(await GetClient<T, IChanReceiverFactory<Unit>, NetChanClientCacheReceiver>(
        chanUri, clientBindingsReceiver, binding, clientReceivers));
    }

    /// <summary>
    /// Gets chan sender from store.
    /// </summary>
    /// <returns>sender end of chan</returns>
    /// <param name="chanUri">uri representing chan: authority will be used for ChanStoreServer lookup 
    /// and path for determining chan.
    /// No authority means local lookup from side of server.
    /// Localhost authority means local lookup from side of client.</param>
    /// <param name="binding">if needs special binding; ignored if cached</param>
    /// <typeparam name="T">type of messagse in chan</typeparam>
    public async Task<IChanSender<T>> GetSenderAsync<T>(Uri chanUri, Binding binding = null) {
      ChanUriValidation(chanUri);

      return string.IsNullOrEmpty(chanUri.Authority)
        ? GetLocalSender<T>(chanUri.AbsolutePath) 
          : GetSenderAndRememberToFree<T>(await GetClient<T, IChanSenderFactory<Unit>, NetChanClientCacheSender>(
        chanUri, clientBindingsSender, binding, clientSenders));
    }

    ///core for getting remote chans (from cache / connect / ...)
    /// - tries assigning bindings in order: provided, default for Uri, default
    Task<T> GetClient<TMsg, T, TC>(Uri chanUri, IDictionary<Uri, Binding> dfltB,
                                   Binding binding, IDictionary<Type, TC> cache) 
      where TC : NetChanClientCache<T> {
      Binding bindDflt;
      dfltB.TryGetValue(chanUri, out bindDflt); //null is fine: would blow, but potentially unnecessary
      TC factoryCache;
      if (cache.TryGetValue(typeof(TMsg), out factoryCache)) //cache: type -> clientCache -> factory -> ret
        return factoryCache.GetAsync(chanUri, binding ?? bindDflt ?? DefaultBinding);
      throw new InvalidOperationException("Client type not initialized");
    }

    #endregion

    #region free and close

    bool Free(Type t, IChanBase chan) {
      //type in the end unnecessary... REMOVE?
      Func<bool> remover;
      return freeChans.TryGetValue(chan, out remover) && remover();
    }

    ///Notify system to no longer send messages to this chan.
    ///(Doesn't really make as much sense as I had hoped in the beginning)
    public bool Free<T>(IChanSender<T> chan) {
      return Free(typeof(T), chan);
    }

    ///Notify system to no longer send messages to this chan.
    ///(Doesn't really make as much sense as I had hoped in the beginning)
    public bool Free<T>(IChanReceiver<T> chan) {
      return Free(typeof(T), chan);
    }

    ///closes all chans; cleares cache; returns accumulated Close etc...
    public Task CloseAll() {
      return CloseAll(false);
    }

    ///closes all chans; cleares cache; returns accumulated Close etc...
    public Task CloseAll(bool requireAllFreed) {
      if (requireAllFreed && freeChans.Count != 0)
        throw new InvalidOperationException("There are still unfreed chans. (count: " + freeChans.Count + ")");
      
      List<Func<bool>> freeClosers;
      lock (freeChans)
        freeClosers = freeChans.Values.ToList();
      foreach (var fv in freeClosers) //free everything
        fv(); //THOUGHT: what would something retuning false mean? - it's always the correct chan...
      //^ it would mean it's probably just ChanWrapFactory...

      return Task.WhenAll(Combine(
        from l in locals
            select l.Value.Chan.Close(),
        from l in locals
            where l.Value.IsNetChan
            select l.Value.Server.Close(),
        from kv in clientReceivers
            from f in kv.Value.All()
            select f.Close(),
        from kv in clientSenders
            from f in kv.Value.All()
            select f.Close()));
    }

    static IEnumerable<T> Combine<T>(params IEnumerable<T>[] seqs) {
      return seqs.SelectMany(x => x);
    }

    #endregion

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

    ///true == client has to be on same IP as WCF request
    ///(default: true)
    public bool LimitClientIP { get; set; }

    ///this HAS to be called only from within WCF ~handler; 
    ///uses some WCF global state thing
    IPAddress AddressToListenOn() {
      if (!LimitClientIP)
        return IPAddress.Any;

      RemoteEndpointMessageProperty endpointP = //address of sender (who requests chan)
        OperationContext.Current.IncomingMessageProperties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
      return endpointP == null ? IPAddress.Any : IPAddress.Parse(endpointP.Address);
    }

    Task<TcpClient> NetChanListen(out int port) { 
      var addr = AddressToListenOn();
      var listener = new TcpListener(addr, 0);//0 == some not used port
      listener.Start(); //initializes socket and assignes port
      port = ((IPEndPoint) listener.LocalEndpoint).Port;
      return TcpListenerAcceptOne(listener);
    }

    //accepts first tcp-client and closes listener
    //if no client in connectingServerTimeout: throws
    async Task<TcpClient> TcpListenerAcceptOne(TcpListener listener) {
      var cT = listener.AcceptTcpClientAsync();
      var timeout = Task.Delay(connectingServerTimeout);
      if (await Task.WhenAny(cT, timeout) == timeout)
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

    ///WCF handler
    NetChanConnectionInfo INetChanProvider.RequestSender(Uri chanName) {
      return Request(chanName, x => x.StartSenderCounterpart);
    }

    ///WCF handler
    NetChanConnectionInfo INetChanProvider.RequestReceiver(Uri chanName) {
      return Request(chanName, x => x.StartReceiverCounterpart);
    }

    #endregion

    //to store both local chan factory and (possibly) chan server of the same chan
    protected sealed class ChanBox {
      public IChanFactory<Unit> Chan { get; private set; }

      public NetChanServer Server { get; private set; }

      public ChanBox(IChanFactory<Unit> local, NetChanServer server) {
        Chan = local;
        Server = server;
      }

      public ChanBox(IChanFactory<Unit> local) : this(local, null) {
      }

      public bool IsNetChan{ get { return Server != null; } }
    }
  }

  ///Determines how will be messages in channels with multiple receivers distributed.
  public enum ChanDistributionType {
    //a la event
    Broadcast,
    //a la workload distribution
    FirstOnly
  }
}

