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
    Dictionary<string, IChanFactory<Nothing>> locals = new Dictionary<string, IChanFactory<Nothing>>();
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
    public Task CreateLocal<T>(string name, ChanDistributionType type = ChanDistributionType.FirstOnly) {
      if (type == ChanDistributionType.FirstOnly) {
        locals.Add(name, new ChanFactoryWrap<T>(new ChanAsync<T>()));
      }
      throw new NotImplementedException();
    }
    //    public Task CreateLocalQueued(string name, ChanDistributionType type, int queueSize) {
    //      throw new NotImplementedException();
    //    }
    public Task CreateNetSender<T>(string name, ChanDistributionType type = ChanDistributionType.FirstOnly) {
      throw new NotImplementedException();
    }

    public Task CreateNetReceiver<T>(string name, ChanDistributionType type = ChanDistributionType.FirstOnly) {
      throw new NotImplementedException();
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
    //    static TR GetWrongTypeThrow<T, TR>(Type chanActualType, Type genericIChan) {
    //      throw new TypeAccessException("wrong chan type({0}); actual: {1}".Format(
    //        typeof(T), GetWrongTypeThrowNameCore(chanActualType, genericIChan)));
    //    }
    //
    //    static string GetWrongTypeThrowNameCore(Type chanActualType, Type genericIChan) {
    //      //just reflection to get generic types of chan for Exception message (own method => single JIT)
    //      return string.Join(" / ", from ifce in chanActualType.GetInterfaces()
    //        where ifce.IsGenericType && ifce.GetGenericTypeDefinition() == genericIChan/*generic variant is genericIChan*/
    //        select ifce.GetGenericArguments()[0].Name/*actual generic type names*/);
    //    }
    static TR GetWrongTypeThrow<T, TR>(Type actualGenericType) {
      throw new TypeAccessException("wrong chan type({0}); actual: {1}".Format(
        typeof(T), actualGenericType.Name));
    }

    IChanReceiver<T> GetLocalReceiver<T>(string chanName) {
      IChanFactory<Nothing> chanF; //null if not exists; wrong type: exception
      return !locals.TryGetValue(chanName, out chanF) ? null 
        : chanF.GetReceiver<T>(null) ?? GetWrongTypeThrow<T, IChanReceiver<T>>(chanF.GenericType);
    }

    IChanSender<T> GetLocalSender<T>(string chanName) {
      IChanFactory<Nothing> chanF; //null unless exists; wrong type: exception
      return !locals.TryGetValue(chanName, out chanF) ? null 
        : chanF.GetSender<T>(null) ?? GetWrongTypeThrow<T, IChanSender<T>>(chanF.GenericType);
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
      return NetChanListenAcceptOne(listener);
    }

    async Task<TcpClient> NetChanListenAcceptOne(TcpListener listener) {
      var c = await listener.AcceptTcpClientAsync();
      listener.Stop();
      return c;
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
    //used to hold info about server net-chans that are not necessarily connected / ...
    internal class NetChanServerInfo {
      //has: interface to server
      // - interface to "outside" (local)
      // - chan name (absolute path of local uri)
      // - ChanDistributionType
      // interfaces are methods that return chans:
      // - It returns the same iff type == firstOnly; a different one (each fed from tee) for BC
      public ChanDistributionType Type { get; private set; }

      public string Name{ get; private set; }

      ///does not include netIn, netOut
      public NetChanConfig Config { get; private set; }
    }
  }

  public enum ChanDistributionType {
    Broadcast,
    FirstOnly
  }
}

