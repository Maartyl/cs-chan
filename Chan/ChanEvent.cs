using System;
using System.Threading.Tasks;

namespace Chan
{
  //consumes channel, invoking event for each message
  //This is the only way to assure that everyone will erceive everything
  public class ChanEvent<TMsg> : IChanBase {
    readonly IChanReceiver<TMsg> chan;
    readonly Task over;

    public event Action<TMsg> ReceivedMessage = x => {};

    /// <summary>
    /// Initializes a new instance of the <see cref="Chan.ChanEvent`1"/> class.
    /// </summary>
    /// <param name="chan">Chan.</param>
    /// <param name="defaultHandler">Will listen since before first receiving of any message.</param>
    public ChanEvent(IChanReceiver<TMsg> chan, Action<TMsg> defaultHandler) {
      if (chan == null)
        throw new ArgumentNullException("chan");
      this.chan = chan;
      ReceivedMessage += defaultHandler;
      over = startListening();
    }

    public ChanEvent(IChanReceiver<TMsg> chan) : this(chan, null) {
    }

    async Task startListening() {
      try {
        while (true) {      
          ReceivedMessage(await chan.ReceiveAsync());
          DebugCounter.Incg(this, "event");
        }
      } catch (TaskCanceledException) {
        //over
        DebugCounter.Incg(this, "over");
      }
      await chan.Close();
    }

    #region IChanBase implementation

    public async Task Close() {
      await chan.Close();
      await AfterClosed();
    }

    public Task AfterClosed() {
      return over;
    }

    #endregion
  }
}

