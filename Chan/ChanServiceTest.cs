using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace ChanWebTest
{
  public class ChanServiceTest : IChanServiceTest {
    public string TestMessage(string name) {
      return string.Format("[{0}] Hello, {1}!", DateTime.Now, name);
    }
  }
}


