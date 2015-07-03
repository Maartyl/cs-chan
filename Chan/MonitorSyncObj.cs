// using System.Threading.Tasks;
using System;
using System.Threading;

namespace Chan
{
  public class MonitorSyncObj {
    public readonly Thread OwningThread;

    protected MonitorSyncObj(Thread t) {
      OwningThread = t;
    }

    public void Enter

    public static MonitorSyncObj Create() {
      return new MonitorSyncObj(Thread.CurrentThread);
    }
    public static MonitorSyncObj CreateAndEnter() {
      var x = Create();

    }
  }
}

