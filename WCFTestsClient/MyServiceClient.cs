using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Service;

//I could make any other same looking interface instead of referencing "service" ns project
//channel probably needs to be of the original interface...
namespace Client
{
  public class MyServiceClient : ClientBase<IGreeterWcfService>, IGreeterWcfService {
    public MyServiceClient(Binding binding, EndpointAddress address) : base (binding, address) {
    }

    public string Greet(string name) {
      return Channel.Greet(name);
    }
  }
}