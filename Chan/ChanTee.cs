using System.Threading.Tasks;
using System;

namespace Chan
{
  public class ChanTee<T> : IChanReceiver<T> {
    IChanReceiver<T> chanIn;
    Chan<T> chanOut = new ChanAsync<T>();
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
        while (true) {      
          var msg = await chanIn.ReceiveAsync();
          var cT = chanOut.SendAsync(msg);
          Message(msg);
          await cT;
        }
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

