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

    public static MonitorSyncObj Create() {
      return new MonitorSyncObj(Thread.CurrentThread);
    }
  }
}

