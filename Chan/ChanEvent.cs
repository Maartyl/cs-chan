using System;
using System.Threading.Tasks;

namespace Chan
{
  //consumes channel, invoking event for each message
  //This is the only way to assure that everyone will erceive everything
  public class ChanEvent<TMsg> {
    IChanReceiver<TMsg> chan;

    event Action<TMsg> ReceivedMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="Chan.ChanEvent`1"/> class.
    /// </summary>
    /// <param name="chan">Chan.</param>
    /// <param name="defaultHandler">Will listen since before first receiving of any message.</param>
    public ChanEvent(IChanReceiver<TMsg> chan, Action<TMsg> defaultHandler) {
      if (chan == null)
        throw new ArgumentNullException("chan");
      this.chan = chan;
      ReceivedMessage = (x => {}) + defaultHandler;
      startListening();
    }

    public ChanEvent(IChanReceiver<TMsg> chan) : this(chan, null) {
    }

    async void startListening() {
      //TMsg msg;
      try {
        while (true) {      
          //        Action<TMsg> a = ReceivedMessage;
          //        await Task.Factory.FromAsync(a.BeginInvoke(msg), a.EndInvoke); //TODO: simulateously receive and invoke
          ReceivedMessage(await chan.ReceiveAsync());
        }
      } catch (TaskCanceledException ex) {
        //over
      }

    }
  }
}

