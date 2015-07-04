using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Collections;

namespace Chan
{
  [TestFixture()]
  public class ChanSimpleTest {
    [Test()]
    public void AllPassedQueued() {
      AllPassed(new ChanQueued<int>());
    }

    [Test()]
    public void AllPassedBuffered() {
      AllPassed(new ChanQueued<int>(400));
    }

    [Test()]
    public void AllPassedBlocking() {
      AllPassed(new ChanBlocking<int>());
    }

    public void AllPassed(Chan<int> chan) {
      Action a = async () => {
        for (int i = 0; i <1000; ++i)
          await chan.SendAsync(i);
      };
      int rslt = 0;
      var x = AllPassedAsyncSum(chan, 10000);
      Action k = async () => rslt = await x;
      k();
      Parallel.Invoke(a, a, a, a, a, a, a, a, a, a);
      Task.WaitAll(chan.Close(), x);
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
          //await Task.Delay(1);
          sum += await ra2;
          //Console.WriteLine("sum step: " + sum + " (i: " + i + "/" + count + ")");
        }
      } catch (TaskCanceledException) {

      }
      Console.WriteLine("sum over");
      return sum;
    }

    [Test()]
    public void OrderWithSingleInAndOutQueued() {
      OrderWithSingleInAndOut(new ChanQueued<int>());
    }

    [Test()]
    public void OrderWithSingleInAndOutBuffered() {
      OrderWithSingleInAndOut(new ChanQueued<int>(500));
    }

    [Test()]
    public void OrderWithSingleInAndOutBlocking() {
      OrderWithSingleInAndOut(new ChanBlocking<int>());
    }

    public void OrderWithSingleInAndOut(Chan<int> chan) {
      //this is needed: tests work correctly otherwise... this initialzes thread pool or something...
      Parallel.Invoke(Task.Delay(4).Wait, Task.Delay(3).Wait, Task.Delay(5).Wait);

      Func<Task> af = async () => {
        for (int i = -500; i < 50000; ++i)
          await chan.SendAsync(i);
        DebugCounter.Incg("test.order", "all sent");
        await chan.Close();
      };
      Func<Task> bf = async () => {
        int prev;
        int cur = int.MinValue;
        try {
          while (true) {
            //Console.Error.WriteLine("" + chan.GetHashCode() + cur);
            prev = cur;
            cur = await chan.ReceiveAsync();
            if (cur < prev)
              Assert.GreaterOrEqual(cur, prev);
          }
        } catch (TaskCanceledException) {
          DebugCounter.Incg("test.order", "cancelled");
        }
      };
      Task at = null;
      Task bt = null;
      Action a = () => at = af();
      Action b = () => bt = bf();
      Parallel.Invoke(b, a);
      Task.WaitAll(at, bt);
    }
  }
}

