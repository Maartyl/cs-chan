using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace Chan
{
  internal abstract class NetChanClientCache<T> { /*where T : class*/
    readonly Dictionary<Uri, T> cache = new Dictionary<Uri, T>();
    readonly Dictionary<Uri, Task<T>> connecting = new Dictionary<Uri, Task<T>>();
    readonly object cacheLock = new object();
    readonly protected TaskCollector clientStarts = new TaskCollector(prematureCompletion: true);

    protected NetChanClientCache() {

    }

    public Task CollectedExceptions{ get { return clientStarts.Task; } }

    ///creates local Sender/ Receiver based on Cache type
    protected abstract T RequireConnect(TcpClient c, NetChanConnectionInfo info, Uri chan);

    ///either GetSender or GetReceiver
    protected abstract NetChanConnectionInfo Request(INetChanProvider p, Uri chanLocalUri);

    NetChanConnectionInfo RequireInfoFromUri(Uri chan, Binding binding) {
      var chanLocalUri = new UriBuilder(chan);
      chanLocalUri.Host = "";
      chanLocalUri.Port = 0;

      var serverAddress = new UriBuilder(chan);
      serverAddress.Path = "";
      serverAddress.Scheme = "http"; //TODO: add some more generic variant
      serverAddress.Query = "";
      serverAddress.Fragment = "";

      var address = new EndpointAddress(serverAddress.Uri);
      var client = new NetChanProviderClient(binding, address);
      var info = Request(client, chanLocalUri.Uri);
      client.Close();
      return info;
    }

    Task<T> RequireAsync(Uri chan, Binding binding) {
      //idea: in lock either:
      // - if already loaded: return that
      // - if already connecting: return that
      // - otherwise: start connecting (runs outside lock) 
      //   - store connecting task (so it can be checked... step 2)
      //   - and at the end of that: 
      //     - in lock: remove self from 'connecting' and write result to cache

      Task<T> tt;//rslt if connecting meanwhile
      T t; //rslt if became available meanwhile
      lock (cacheLock)
        return connecting.TryGetValue(chan, out tt) ? tt
          : cache.TryGetValue(chan, out t) ? Task.FromResult(t)
            : connecting[chan] = Task.Run(() => {
          //not connecting nor loaded: 'I' will load it (in background): 
          // //does not run inside lock
          var info = RequireInfoFromUri(chan, binding);

          if (info.IsOk) {
            var tcp = new TcpClient(chan.Host, info.Port);
            var data = RequireConnect(tcp, info, chan);
            lock (cacheLock) {
              cache[chan] = data;
              connecting.Remove(chan);
            }
            return data;
          }
          //exception / error / not OK
          //remove from connecting without stroring in cache (before throw)
          // -> can be tried again
          lock (cacheLock)
            connecting.Remove(chan);
          
          if (info.ErrorType != null && info.ErrorMessage != null) {
            //if exception as: type + message
            Type type = Type.GetType(info.ErrorType);
            if (type != null && type.IsSubclassOf(typeof(Exception))) {
              ConstructorInfo ctor = type.GetConstructor(new[] { typeof(string) });
              if (ctor != null)
                throw (Exception) ctor.Invoke(new[] { info.ErrorMessage });
            }
          }
          throw new NetChanProviderException(info);//TODO: include chan::Uri ?
        });
    }

    T RequireWait(Uri chanName, Binding binding) {
      //Yes, async would be better, but I don't want ChanStore.Get{Sender,Receiver} to return Task...
      //wait until loaded: if loaded here: possible deadlock (thus: Task.Run) - always 'background' thread
      return RequireAsync(chanName, binding).Result;
    }

    public T Get(Uri chanName, Binding binding) {
      T data;
      bool isInCache;
      lock (cacheLock)
        isInCache = cache.TryGetValue(chanName, out data);

      return isInCache ? data : RequireWait(chanName, binding);
    }

    public Task<T> GetAsync(Uri chanName, Binding binding) {
      T data;
      bool isInCache;
      lock (cacheLock)
        isInCache = cache.TryGetValue(chanName, out data);

      return isInCache ? Task.FromResult(data) : RequireAsync(chanName, binding);
    }
    //TODO: forget(Uri) = lock, cache.Remove
  }
}

