using System;
using System.Threading.Tasks;

namespace Chan
{
  class MainClass {
    public static void Main(string[] args) {
      var test = new ChanSimpleTest();
      //test.AllPassed();
      test.OrderWithSingleInAndOut();

      var cs = new ChanQueued<String>();
      new ChanEvent<String>(cs, Console.WriteLine);

      exec(cs).Wait();
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
  }
}
