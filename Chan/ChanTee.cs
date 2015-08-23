using System.Threading.Tasks;
using System;

namespace Chan
{
  public class ChanTee<T> : IChanReceiver<T> {
    IChanReceiver<T> chanIn;
    ChanBase<T> chanOut = new ChanAsync<T>();
    readonly Task over;

    event Action<T> Message = a => {};

    public ChanTee(IChanReceiver<T> chan) {
      if (chan == null)
        throw new ArgumentNullException("chan");
      this.chanIn = chan;
      over = startListening();
    }

    public ChanTee(IChanReceiver<T> chan, Action<T> handler) {
      if (chan == null)
        throw new ArgumentNullException("chan");
      this.chanIn = chan;
      Message += handler;
      over = startListening();
    }

    async Task startListening() {
      //TMsg msg;
      try {
        while (true)       
          Message(await chanIn.ReceiveAsync(chanOut.SendAsync));
      } catch (TaskCanceledException) {
        //over (either side closed)
      }
      await chanIn.Close(); //cannot use Close(): deadlock (cycle)
      await chanOut.Close();
    }
    #region IChanReceiver implementation
    public Task<T> ReceiveAsync() {
      return chanOut.ReceiveAsync();
    }

    public Task<T> ReceiveAsync(Func<T, Task> sendCallback) {
      return chanOut.ReceiveAsync(sendCallback);
    }
    #endregion
    #region IChanBase implementation
    public async Task Close() {
      await chanIn.Close(); //order matters: could still receive something
      await chanOut.Close();
      await AfterClosed();
    }

    public Task AfterClosed() {
      return over;
    }
    #endregion
  }
}

