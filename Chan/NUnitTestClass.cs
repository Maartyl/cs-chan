using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Collections;

namespace Chan
{
  [TestFixture()]
  public class ChanSimpleTest {
    //    [Test()]
    //    public void AllPassedQueued() {
    //      AllPassed(new ChanQueued<int>());
    //    }
    //
    //    [Test()]
    //    public void AllPassedBuffered() {
    //      AllPassed(new ChanQueued<int>(400));
    //    }
    //
    //    [Test()]
    //    public void AllPassedBlocking() {
    //      AllPassed(new ChanBlocking<int>());
    //    }
    [Test()]
    public void AllPassedAsync() {
      AllPassed(new ChanAsync<int>());
    }

    public void AllPassed(Chan<int> chan) {
      Func<Task> a = async () => {
        for (int i = 0; i <1000; ++i)
          await chan.SendAsync(i);
        await chan.Close();
      };
      var x1 = AllPassedAsyncSum(chan, 10000 / 4);
      var x2 = AllPassedAsyncSum(chan, 10000 / 4);
      var x3 = AllPassedAsyncSum(chan, 10000 / 4);
      var x4 = AllPassedAsyncSum(chan, 10000 / 4);
      int rslt = 0;
      Action k = async () => rslt = await x1 + await x2 + await x3 + await x4;
      k();
      Task.WaitAll(a(), a(), a(), a(), a(), a(), a(), a(), a(), a(), x1, x2, x3, x4);
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
    //    [Test()]
    //    public void OrderWithSingleInAndOutQueued() {
    //      OrderWithSingleInAndOut(new ChanQueued<int>());
    //    }
    //
    //    [Test()]
    //    public void OrderWithSingleInAndOutBuffered() {
    //      OrderWithSingleInAndOut(new ChanQueued<int>(500));
    //    }
    //
    //    [Test()]
    //    public void OrderWithSingleInAndOutBlocking() {
    //      OrderWithSingleInAndOut(new ChanBlocking<int>());
    //    }
    [Test()]
    public void OrderWithSingleInAndOutAsync() {
      OrderWithSingleInAndOut(new ChanAsync<int>());
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
            prev = cur;
            cur = await chan.ReceiveAsync();
            if (cur < prev)
              Assert.GreaterOrEqual(cur, prev);
          }
        } catch (TaskCanceledException) {
          DebugCounter.Incg("test.order", "cancelled");
        }
      };
//      Task at = null;
//      Task bt = null;
//      Action a = () => at = af();
//      Action b = () => bt = bf();
//      Parallel.Invoke(b, a);
//      Task.WaitAll(at, bt);
      Task.WaitAll(af(), bf());
    }
  }

  [TestFixture]
  public class NetChanTest : NetChanBase {

    #region implemented abstract members of NetChanBase
    public NetChanTest():base(new NetChanConfig()) {

    }

    public override Task Start(uint key) {
      throw new NotImplementedException();
    }

    protected override Task<Header> OnMsgReceived(Header h) {
      throw new NotImplementedException();
    }

    protected override Task OnCloseReceived(Header h) {
      throw new NotImplementedException();
    }
    #endregion
    [Test]
    public void HeaderToString() {
      //I know, this is not a proper test...
      Console.WriteLine(Header.Close);
      Console.WriteLine(Header.Ping);
      Console.WriteLine(Header.Pong);
      Console.WriteLine(CreateBaseMsgHeader());
      Console.WriteLine(CreateBaseMsgHeader());
      Console.WriteLine(new Header(Header.Op.Err) { ErrorCode = 45, Length = 100 });
      Console.WriteLine(new Header(Header.Op.Open) { Key=789564 });
      Console.WriteLine(Header.AckFor(CreateBaseMsgHeader()));
    }
  }
}

