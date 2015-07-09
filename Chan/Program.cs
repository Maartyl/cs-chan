using System;
using System.Threading.Tasks;
using System.Threading;

namespace Chan
{
  public class MainClass {
    public static void Main(string[] args) {
      var ctknSrc = new CancellationTokenSource();
      PeriodicCheckDebugCounter(ctknSrc.Token);

      var testNet = new NetChanTest();
      testNet.HeaderToString();

      var test = new ChanSimpleTest();
      Console.WriteLine("start");

      test.AllPassedAsync();
      Console.WriteLine("all");
      DebugCounter.Glob.Print(Console.Error);
      DebugCounter.Glob.Clear();

      test.OrderWithSingleInAndOutAsync();
      //test.OrderWithSingleInAndOutBuffered();
      //test.OrderWithSingleInAndOutQueued();
      Console.WriteLine("order");
      DebugCounter.Glob.Print(Console.Error);
      DebugCounter.Glob.Clear();

      var chan = new ChanAsync<String>();
      new ChanEvent<String>(chan, Console.WriteLine);

      DebugCounter.Glob.Print(Console.Error);
      ctknSrc.Cancel();

      exec(chan).Wait();
      DebugCounter.Glob.Print(Console.Error);
    }

    static async Task exec(IChanSender<String> cs) {
      await cs.SendAsync("Hello");
      await cs.SendAsync("from");
      await cs.SendAsync("async channel");
      string s;
      while ((s = Console.ReadLine()) != null)
        await cs.SendAsync(s);
      await cs.Close();
      try {
        await cs.SendAsync("this should not be shown");
      } catch (TaskCanceledException) {
        Console.WriteLine("correctly thrown cancelled");
      }
    }

    static async void PeriodicCheckDebugCounter(CancellationToken ctkn) {
      try {
        while (!ctkn.IsCancellationRequested) {
          await Task.Delay(10 * 1000, ctkn);
          DebugCounter.Glob.Print(Console.Error);
        }
      } catch (TaskCanceledException) {
        
      }
    }
  }
}
