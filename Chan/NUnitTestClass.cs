using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Collections;

namespace Channels
{
  [TestFixture()]
  public class ChanSimpleTest {
    [Test()]
    public void AllPassed() {
      var cs = new ChanQueued<int>();

      Action a = async () => {
        for (int i = 0; i <1000; ++i)
          await cs.SendAsync(i);
      };
      int rslt = 0;
      var x = AllPassedAsyncSum(cs, 10000);
      Action k = async () => rslt = await x;
      k();
      Parallel.Invoke(a, a, a, a, a, a, a, a, a, a);
      cs.Close();
      x.Wait();
      Assert.AreEqual(4995000, rslt);
    }

    private async Task<int> AllPassedAsyncSum(IChanReceiver<int> cs, int count) {
      Assert.IsTrue((count&1) == 0, "async sum; Requires even count");
      int sum = 0;
      Console.WriteLine("sum start");
      count = count / 2;
      try {
        for (int i = 0; i < count; ++i) {
          var ra1 = cs.ReceiveAsync();
          var ra2 = cs.ReceiveAsync();
          sum += await ra1;
          sum += await ra2;
          //Console.WriteLine("sum step: " + sum + " (i: " + i + "/" + count + ")");
        }
      } catch (TaskCanceledException) {

      }
      Console.WriteLine("sum over");
      return sum;
    }
  }
}

