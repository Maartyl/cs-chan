using System;
using Chan;
using System.Threading.Tasks;
using System.Threading;
using System.ServiceModel;

namespace Chat
{
  public class ChatClient {
    readonly Settings settings;
    readonly ChanStore store;
    readonly Connector connector;
    volatile State state = State.Disconnected;
    Action afterConnected;
    ConnectionChans chans;

    public ChatClient(Settings settings, ChanStore store, Connector connector) {
      this.settings = settings;
      this.connector = connector;
      this.store = store;
      ClientName = settings.ClientDefaultName;
    }

    public string ClientName { get; set; }

    Task Send(Message msg) {
      var s = state;
      var bc = chans.Broadcast;
      if (s == State.Connected && bc != null)
        return bc.SendAsync(msg);
      else
        //shouldn't get here...
        throw new InvalidOperationException("(inner:) disconnected or no broadcast-chan");
    }

    public async void Connect(CmdArg arg) {
      string addr = arg;
      try {
        //check state
        switch (state) {
          case State.Connected:
            connector.RunError("already connected (" + chans.MetaAddr + ")".ArgSrc("connect"));
            return;
          case State.Connecting:
            connector.RunNotifySystem("connecting...".ArgSrc("connect"));
            return;
          case State.ConnectingFailed:
            connector.RunError("connecting failed; disconnected".ArgSrc("connect"));
            return;
          case State.Disconnected:
            //continue
          break;
        }
        //free Gui thread
        await Task.Yield();

        //possibly append port if necessary
        if (!addr.Contains(":")) {
          if (settings.DefaultServerPort == -1)
            throw new ArgumentException("connect: requires addr:port (no default port)");
          else
            addr += ":" + settings.DefaultServerPort;
        }
        //create uri and helpers (uri from path; 
        var uri = new Uri("chan://" + addr + "");
        Func<string,Uri> path = s => new UriBuilder(uri){ Path = s }.Uri;

        //run after connected (or failed)
        Action<State> cleanConnecting = (s) => {
          if (state == State.Connected && s == State.ConnectingFailed)
            //exception in a in OK connected
            return;
          state = s; //change state before getting afterC: cannot add anything else
          Action a = afterConnected;
          try {
            if (a != null)
              a();
          } finally {
            afterConnected = null;
          }
        };

        //connecting itself 
        state = State.Connecting;
        try {
          chans = await ConnectionChans.Connect(store, path);
          chans.MetaAddr = addr;
          cleanConnecting(State.Connected);
        } catch (Exception) {
          cleanConnecting(State.ConnectingFailed);
          throw;
        }

        //receive messages loop
        ChanEvent.Listen(chans.Broadcast, ReceiveMessage);

        //inform: client connected
        BroadcastMessage(new Message(Message.MessageType.Connected, "".ArgSrc(ClientName))); 

      } catch (EndpointNotFoundException ex) {
        connector.RunError("no server found".ArgSrc("connect " + addr));
      } catch (Exception ex) {
        ex.PipeEx(connector, "connect " + addr);
      } finally {
        if (state != State.Connected)
          state = State.Disconnected;
      }
    }

    //used from ChanEvent
    void ReceiveMessage(Message msg) {
      switch (msg.Type) {
        case Message.Type.Message:
          connector.RunOrDefault(Cmd.ReceivedMsg, msg.Data);
          return;
        case Message.Type.Connected:
          //someone has conected: notify
          //POSSIBLY: improve (better msg; consider text...)
          connector.RunNotifySystem(msg.Data.Source.ArgSrc("connected"));
          return;
        case Message.Type.Disconnected:
          //someone has disconected: notify
          connector.RunNotifySystem(msg.Data.Source.ArgSrc("disconnected"));
          return;
      }
    }

    public void Disconnect() {
      var msg = new Message(Message.MessageType.Disconnected, "".ArgSrc(ClientName));
      BroadcastMessage(msg, t => {
        state = State.Disconnected;
        chans = chans.Free(store, connector);
      });
    }

    public void BroadcastMessage(Message msg, Action<Task> afterSent = null) {
      switch (state) {
        case State.Connected:
          connector.PipeEx("ChatClient.BcMsg", Send(msg).ContinueWith(afterSent));
          return;
        case State.Connecting:
          connector.RunNotifySystem("connecting...".ArgSrc(Cmd.Send));
          Action orig;
          Action merged;
          do {
            orig = afterConnected;
            merged = orig + (() => BroadcastMessage(msg, afterSent));
            if (state != State.Connecting) {
              BroadcastMessage(msg, afterSent);
              return;
            }
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
      if (msg.Source == null)
        msg = msg.Text.ArgSrc(ClientName);
      BroadcastMessage(new Message(msg));
    }

    public enum State {
      Connected,
      Connecting,
      ConnectingFailed,
      Disconnected
    }

    struct ConnectionChans {
      public IChan<Message> Broadcast{ get; private set; }

      IChanSender<Message> bsSender{ get; set; }

      IChanReceiver<Message> bsReceiver{ get; set; }

      //meta data: address used to connect
      public string MetaAddr{ get; set; }

      public ConnectionChans Free(ChanStore store, Connector conn) {
        Broadcast.Close().PipeEx(conn, "closing channel (" + MetaAddr + ")");
        store.Free(bsSender);
        store.Free(bsReceiver);
        return new ConnectionChans{ };
      }

      public static async Task<ConnectionChans> Connect(ChanStore store, Func<string, Uri> p) {
        var broadcast = p("broadcast"); //channel name
        var rT = store.GetReceiverAsync<Message>(broadcast);
        var sT = store.GetSenderAsync<Message>(broadcast);
        var k = Chan.Chan.Combine(await rT, await sT);
        return new ConnectionChans{ Broadcast = k, bsSender = await sT, bsReceiver = await rT };
      }
    }
  }
}

