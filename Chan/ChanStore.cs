// using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Chan
{
  //allows access to registered channels
  public class ChanStore {
    Dictionary<string, IChanBase> receivers = new Dictionary<string, IChanBase>();
    Dictionary<string, IChanBase> senders = new Dictionary<string, IChanBase>();

    public ChanStore() {

    }

    public void RegisterReceiver<T>(Uri chanUri, IChanReceiver<T> chan) {
      ChanUriValidation(chanUri);
      if (chan == null)
        throw new ArgumentNullException("chan");
      if (chanUri.Authority != "")
        throw new ArgumentException("can only register local chans (is: {0})".Format(chanUri));

      receivers.Add(chanUri.AbsolutePath, chan);
    }

    static TR GetWrongTypeThrow<T, TR>(Type chanActualType, Type genericIChan) {
      throw new TypeAccessException("wrong chan type({0}); actual: {1}".Format(
        typeof(T), GetWrongTypeThrowNameCore(chanActualType, genericIChan)));
    }

    static string GetWrongTypeThrowNameCore(Type chanActualType, Type genericIChan) {
      //just reflection to get generic types of chan for Exception message (own method => single JIT)
      return string.Join(" / ", from ifce in chanActualType.GetInterfaces()
        where ifce.IsGenericType && ifce.GetGenericTypeDefinition() == genericIChan/*generic variant is genericIChan*/
        select ifce.GetGenericArguments()[0].Name/*actual generic type names*/);
    }

    IChanReceiver<T> GetLocalReceiver<T>(string chanName) {
      IChanBase chan; //null unless exists; wrong type: exception
      return !receivers.TryGetValue(chanName, out chan) ? null 
        : chan as IChanReceiver<T> ?? GetWrongTypeThrow<T, IChanReceiver<T>>(chan.GetType(), typeof(IChanReceiver<>));
    }

    IChanSender<T> GetLocalSender<T>(string chanName) {
      IChanBase chan; //null unless exists; wrong type: exception
      return !senders.TryGetValue(chanName, out chan) ? null 
        : chan as IChanSender<T> ?? GetWrongTypeThrow<T, IChanSender<T>>(chan.GetType(), typeof(IChanSender<>));
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
  }
}

