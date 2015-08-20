using System;
using Chan;
using System.Threading.Tasks;
using System.Threading;

namespace Chat
{
  public class ChatClient {
    string clientName;
    readonly Settings settings;
    readonly ChanStore store;
    readonly Connector connector;
    State state = State.Disconnected;
    IChanSender<Message> broadcast;
    Action afterConnected;

    public ChatClient(Settings settings, ChanStore store, Connector connector) {
      this.settings = settings;
      this.connector = connector;
      this.store = store;
      clientName = settings.ClientDefaultName;
    }

    Task Send(Message msg) {
      var bc = broadcast;
      if (bc != null)
        return bc.SendAsync(msg);
      else
        //shouldn't get here...
        throw new InvalidOperationException("(inner:) broadcast chan is null");
    }

    public void Connect(CmdArg arg) {
      
    }

    public void Disconnect(CmdArg arg) {

    }

    public void BroadcastMessage(Message msg) {
      switch (state) {
        case State.Connected:
          connector.PipeEx("ChatClient.BcMsg", Send(msg));
          return;
        case State.Connecting:
          Action orig;
          Action merged;
          do {
            orig = afterConnected;
            merged = orig + (() => BroadcastMessage(msg));
          } while (orig != Interlocked.CompareExchange(ref afterConnected, merged, orig));
          return;
        case State.ConnectingFailed:
          connector.RunError("ConnectingFailed; cannot send: " + msg);
          return;
        case State.Disconnected:
          connector.RunNotifySystem("sending: not connected");
          return;
      }
    }

    public void BroadcastMessage(CmdArg msg) {
      BroadcastMessage(new Message(msg));
    }

    public enum State {
      Connected,
      Connecting,
      ConnectingFailed,
      Disconnected
    }
  }
}

