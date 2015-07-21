using System;
using System.ServiceModel;
using System.Linq;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Reflection;

namespace Chan
{
  internal class NetChanClientCache {
    readonly Dictionary<Uri, NetChanClient> cache = new Dictionary<Uri, NetChanClient>();
    readonly Dictionary<Uri, Task> connecting = new Dictionary<Uri, Task>();
    readonly object cacheLock = new object();

    public NetChanClientCache() {
    }

    IChanSender<T> RequireSender<T>(Uri chan, Binding binding) {
      IChanSender<T> loadedSender = null;
      lock (cacheLock) {
        Task t;//if connecting meanwhile
        if (connecting.TryGetValue(chan, out t)) {
          t.Wait(); //TODO: !!! resolve deadlock: always load in background thread?
          return cache[chan].GetSender<T>();
        }
        NetChanClient ncb; //if became available meanwhile
        if (cache.TryGetValue(chan, out ncb)) {
          return ncb.GetSender<T>();
        }
        //neither: I will load it (in background): 
        connecting[chan] = Task.Run(() => {
          var chanLocalUri = new UriBuilder(chan);
          chanLocalUri.Host = "";
          chanLocalUri.Port = 0;
          var serverAddress = new UriBuilder(chan);
          serverAddress.Path = "";
          serverAddress.Scheme = "http"; //TODO: add more generic variant
          serverAddress.Query = "";
          serverAddress.Fragment = "";
          var address = new EndpointAddress(serverAddress.Uri);
          var client = new NetChanProviderClient(binding, address);
          var info = client.RequestSender(chanLocalUri.Uri);

          if (info.IsOk) {
            //var tcp = new TcpClient(

          } else {

            if (info.ErrorType != null && info.ErrorMessage != null) {
              //TODO: handle exceptions that don't have .ctor(string)
              Type type = Type.GetType(info.ErrorType);
              ConstructorInfo ctor = type.GetConstructor(new[] { typeof(string) });
              throw (Exception) ctor.Invoke(new object[] { info.ErrorMessage });
            }
            if (info.ErrorMessage != null) {
              throw new RemoteException(info.ErrorMessage);
            }
          }

          client.Close();
        });
      }
      //Yes, async would be better, but I don't want ChanStore.Get{Sender,Receiver} to return Task...
      //wait until loaded: if done here: possible deadlock
      connecting[chan].Wait();
      lock (cacheLock) {

      }
      return loadedSender;
    }

    public IChanSender<T> GetSender<T>(Uri chanName, Binding binding) {

    }
  }
}

