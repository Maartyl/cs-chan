// using System.Threading.Tasks;
using System;

namespace Chan
{
  //allows access to registered channels
  public class ChanStore {
    public ChanStore() {

    }

    public TR GetReceiver<TR, T>(Uri chanUri) where TR : IChanReceiver<T> {

    }
  }
}

