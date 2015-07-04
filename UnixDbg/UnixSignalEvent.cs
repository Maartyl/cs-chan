using System;
using System.Threading;
using Mono.Unix;

namespace UnixDbg
{
  public static class UnixSignalEvent {
    static UnixSignal[] signalsUsr1 = new UnixSignal [] {
      new UnixSignal (Mono.Unix.Native.Signum.SIGUSR1),
    };

    public static Thread ListenUsr1(Action action) {
      Thread signalThread = new Thread(() => {
        while (true) {
          //returns index to the passed array
          int index = UnixSignal.WaitAny(signalsUsr1, -1);
          action();
        }
      });
      signalThread.Start();
      return signalThread;
    }
  }
}

