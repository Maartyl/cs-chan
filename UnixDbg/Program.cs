// using System.Threading.Tasks;
using System;
using System.Threading;

namespace UnixDbg
{
  class MainClass {
    public static void Main(string[] args) {
      Thread usr1 = UnixSignalEvent.ListenUsr1(() => Chan.DebugCounter.Glob.Print(Console.Error));
      Chan.MainClass.Main(args);
      usr1.Abort();
    }
  }
}
