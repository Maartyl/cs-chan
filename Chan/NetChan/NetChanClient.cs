using System;

namespace Chan
{
  public abstract class NetChanClient : IChanFactory<Nothing> {
    protected NetChanClient() {
    }
  }

  public class NetChanClient<T> : NetChanClient {

  }
}

