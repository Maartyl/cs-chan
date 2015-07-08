// using System.Threading.Tasks;
using System;

namespace Chan
{
  /// caused by ERR packet in NetChan
  public class RemoteException : Exception {
    public RemoteException(string message):base(message) {
    }
  }
}

