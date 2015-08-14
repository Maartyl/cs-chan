using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Chan
{
  public class NetChanProviderClient : ClientBase<INetChanProvider>, INetChanProvider {
    public NetChanProviderClient(Binding binding, EndpointAddress address) : base (binding, address) {
    }

    public NetChanConnectionInfo RequestReceiver(Uri chanName) {
      return Channel.RequestReceiver(chanName);
    }

    public NetChanConnectionInfo RequestSender(Uri chanName) {
      return Channel.RequestSender(chanName);
    }
  }
}

