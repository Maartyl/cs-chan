using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Chan
{
  //non-generic to be used in dicts
  //TODO: factory is maybe not a good name: often returns the same...
  public interface IChanFactory<TCtor> {
    IChanReceiver<T> GetReceiver<T>(TCtor ctorData);

    IChanSender<T> GetSender<T>(TCtor ctorData);

    Type GenericType{ get; }

    ///if contained and freed
    bool Free(IChanBase chan);
  }
  //senders are generally all gonna be the same, but 
  // \ receivers will be different for broadcast
  public abstract class ChanFactory<T, TCtor> : IChanFactory<TCtor> {
    public abstract IChanReceiver<T> GetReceiver(TCtor ctorData);

    public abstract IChanSender<T> GetSender(TCtor ctorData);

    public abstract ChanDistributionType DistributionType{ get; }
    #region IChanFactory implementation
    public Type GenericType{ get { return typeof(T); } }

    public IChanReceiver<TT> GetReceiver<TT>(TCtor ctorData) {
      return GetReceiver(ctorData) as IChanReceiver<TT>;
    }

    public IChanSender<TT> GetSender<TT>(TCtor ctorData) { 
      return GetSender(ctorData) as IChanSender<TT>;
    }

    public abstract bool Free(IChanBase chan);
    #endregion
  }
  //only wraps: does not provide broadcast
  public class ChanFactoryWrap<T> : ChanFactory<T, Nothing> {
    IChanReceiver<T> chanR;
    IChanSender<T> chanS;

    public ChanFactoryWrap(IChanReceiver<T> chanR, IChanSender<T> chanS) {
      this.chanR = chanR;
      this.chanS = chanS;
    }

    public ChanFactoryWrap(IChan<T> chan): this(chan, chan) {

    }
    #region implemented abstract members of ChanFactory
    public override IChanReceiver<T> GetReceiver(Nothing ctorData) {
      return chanR;
    }

    public override IChanSender<T> GetSender(Nothing ctorData) {
      return chanS;
    }

    public override bool Free(IChanBase chan) {
      //nothing
      return false;
    }

    public override ChanDistributionType DistributionType { get { return ChanDistributionType.FirstOnly; } }
    #endregion
  }
  //this works like events: if noone subscribed, message get's lost
  //receivers do not propagate close
  public class ChanFactoryReceiveAll<T> : ChanFactory<T, Nothing> {
    ChanEvent<T> evt;
    IChanSender<T> chanS;
    Dictionary<IChanBase,Action<T>> receivers;
    volatile bool closedAndEmpty;
    ExceptionDrain drain = new ExceptionDrain();

    public ChanFactoryReceiveAll(IChanReceiver<T> chanR, IChanSender<T> chanS) {
      this.evt = new ChanEvent<T>(chanR);
      this.chanS = chanS;
      drain.Consume(chanR.AfterClosed().ContinueWith(t => {
        closedAndEmpty = true;
        drain.Consume(Task.WhenAll(receivers.Select(kv => kv.Key.Close())));
      }));
    }
    #region implemented abstract members of ChanFactory
    public override IChanReceiver<T> GetReceiver(Nothing ctorData) {
      if (closedAndEmpty)
        return Chan.Closed<T>();

      var c = new ChanAsync<T>();
      Action<T> self = null; //ref to e
      Action<T> eh = async t => {
        var s = c.SendAsync(t);
        try {
          await s;
        } catch (TaskCanceledException) {
          evt.ReceivedMessage -= self;
        } catch (Exception) {
          drain.Consume(s);
          throw;
        }};
      self = eh;
      receivers.Add(c, self);
      evt.ReceivedMessage += self;

      return c;
    }

    public override IChanSender<T> GetSender(Nothing ctorData) {
      return chanS;
    }

    public override bool Free(IChanBase chan) {
      Action<T> a;
      if (receivers.TryGetValue(chan, out a)) {
        evt.ReceivedMessage -= a;
        receivers.Remove(chan);
        return true;
      }
      return false;
    }

    public override ChanDistributionType DistributionType { get { return ChanDistributionType.Broadcast; } }
    #endregion
  }
}

