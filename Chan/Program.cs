using System;
using System.Threading.Tasks;

namespace Channels
{
  class MainClass {
    public static void Main(string[] args) {

      new ChanSimpleTest().AllPassed();

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
      cs.Close();
      await cs.SendAsync("this should not be shown");
    }
  }
}
