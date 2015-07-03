using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Collections;

namespace Chan
{
  [TestFixture()]
  public class ChanSimpleTest {
    [Test()]
    public void AllPassed() {
      var cs = new ChanSimple<int>();

      Action a = async () => {
        for (int i = 0; i <100; ++i)
          await cs.SendAsync(i);
      };
      int rslt = 0;
      Parallel.Invoke(a, a, a, a, a, a, a, a, a, a, async () => rslt = await AllPassedAsyncSum(cs, 1000));
      cs.Close();
      Assert.AreEqual(49500, rslt);
    }

    private async Task<int> AllPassedAsyncSum(IChanReceiver<int> cs, int count) {
      Assert.IsTrue((count&1) == 0, "async sum; Requires even count");
      int sum = 0;
      try {
        for (int i = 0; i < count; ++i) {
          var ra1 = cs.ReceiveAsync();
          var ra2 = cs.ReceiveAsync();
          sum += await ra1;
          sum += await ra2;
        }
      } catch (TaskCanceledException ex) {
        return sum;
      }
      return sum;
    }
  }
}

