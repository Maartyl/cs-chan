// using System.Threading.Tasks;
using System;

namespace UnixDbg
{
  class MainClass {
    public static void Main(string[] args) {
      UnixSignalEvent.ListenUsr1(() => Chan.DebugCounter.Glob.Print(Console.Error));
      Chan.MainClass.Main(args);
    }
  }
}
