using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace ChanWebTest
{
  [ServiceContract]
  public interface IChanServiceTest {
    [OperationContract]
    string TestMessage(string name);
  }
}

