// using System.Threading.Tasks;
using System;

namespace Chan
{
  //non-generic to be used in dicts
  //TODO: factory is maybe not a good name: often returns the same...
  public interface IChanFactory<TCtor> {
    IChanReceiver<T> GetReceiver<T>(TCtor ctorData);

    IChanSender<T> GetSender<T>(TCtor ctorData);

    Type GenericType{ get; }

    bool Free(IChanBase chan);
  }

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
    }

    public override ChanDistributionType DistributionType { get { return ChanDistributionType.FirstOnly; } }
    #endregion
  }
}

