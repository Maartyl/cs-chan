﻿using System;
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

    string clientName;

    public string ClientName {
      get {
        return clientName;
      }
      set {
        if (string.IsNullOrWhiteSpace(value)) {
          connector.RunError("cannot change name to: " + value);
          return;
        }
        value = value.Trim();

        if (state == State.Connected) {
          var msg = new Message(Message.MessageType.SysMessage,
                      string.Format("\"{0}\" to \"{1}\"", clientName, value).ArgSrc("rename"));
          if (state == State.Connected)
            //if: in case it's changed meanwhile
            //I don't care much if it happens 'right now' ad fails : this is just a test app
            BroadcastMessage(msg);
        }
        clientName = value;
      }
    }

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
      string addr = arg.Text ?? "localhost";
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
        //await Task.Yield();

        //possibly append port if necessary
        if (!addr.Contains(":")) {
          if (settings.DefaultServerPort == -1)
            throw new ArgumentException("connect: requires addr:port (no default port)");
          else
            addr += ":" + settings.DefaultServerPort;
        }
        //create uri and helpers (uri from path) 
        var uri = new Uri("chan://" + addr + "");
        Func<string,Uri> path = s => new UriBuilder(uri){ Path = s }.Uri;

        //run after connected (or failed)
        Action<State> cleanConnecting = s => {
          if (state == State.Connected && s == State.ConnectingFailed)
            //exception in afterConnected in OK connected
            return;
          state = s; //change state before getting afterC: ~cannot add anything else

          Action a = null;
          while (true)
            try {
              a = afterConnected; //get and null afterConnected
              afterConnected = null;
              if (a == null)
              //nothing to do or added while executing last time
                return;
              a();
            } finally {
              //: I doubt this works... - It does, actually.
              //worst case scenario: something will be left in afterConnected...
              if (a != null)
                continue;
            }
        };

        //actually connect
        state = State.Connecting;
        try {
          chans = await ConnectionChans.Connect(settings, store, path);
          chans.MetaAddr = addr;
          cleanConnecting(State.Connected);
        } catch (Exception) {
          cleanConnecting(State.ConnectingFailed);
          throw;
        }

        //receive messages loop
        var ce = ChanEvent.Listen(chans.Broadcast, ReceiveMessage);
        chans.MetaEvent = ce;

        //inform: client connected
        BroadcastMessage(new Message(Message.MessageType.Connected, "".ArgSrc(ClientName))); 

      } catch (EndpointNotFoundException) {
        connector.RunError("no server found".ArgSrc("connect " + addr));
        #if DEBUG
        //ex.PipeEx(connector, "connect.notFound " + addr);
        #endif
      } catch (Exception ex) {
        ex.PipeEx(connector, "connect " + addr);
      } finally {
        if (state != State.Connected)
          state = State.Disconnected;
      }
      try {
        if (state != State.Disconnected && chans.BsReceiver != null)
          await chans.BsReceiver.AfterClosed().ContinueWith(t => {
            connector.RunNotifySystem("channel closed".ArgSrc("disconnecting " + addr));
            DisconnectCore();
          });//.PipeEx(connector, "connect[after chan closed] " + addr);
      } catch (TaskCanceledException) {
        //channel closed: already processed in continuation
      } catch (Exception ex) {
        ex.PipeEx(connector, "connect[after chan closed awaiting] " + addr);
      }

    }

    //used from ChanEvent
    void ReceiveMessage(Message msg) {
      switch (msg.Type) {
        case Message.MessageType.Message:
          connector.RunOrDefault(Cmd.ReceivedMsg, msg.Data);
          return;
        case Message.MessageType.SysMessage:
          connector.RunNotifySystem(msg.Data);
          return;
        case Message.MessageType.Connected:
          //someone has conected: notify
          //POSSIBLY: improve (better msg; consider text...)
          connector.RunNotifySystem(msg.Data.Source.ArgSrc("connected"));
          return;
        case Message.MessageType.Disconnected:
          //someone has disconected: notify
          connector.RunNotifySystem(msg.Data.Source.ArgSrc("disconnected"));
          return;

      }
    }

    public void Disconnect() {
      if (state != State.Connected)
        connector.RunError("not connected".ArgSrc("disconnect"));
      else BroadcastMessage(
          new Message(Message.MessageType.Disconnected,
            "".ArgSrc(ClientName)), t => DisconnectCore());
    }

    void DisconnectCore() {
      state = State.Disconnected;
      chans.MetaEvent.Stop();
      chans = chans.Free(store, connector);
    }

    public void BroadcastMessage(Message msg, Action<Task> afterSent = null) {
      switch (state) {
        case State.Connected:
          var sentT = Send(msg);
          if (afterSent != null)
            sentT = sentT.ContinueWith(afterSent);
          connector.PipeEx("ChatClient.BcMsg", sentT);
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
          connector.RunError("sending: not connected");
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
      public IChan<Message> Broadcast { get; private set; }

      public IChanSender<Message> BsSender { get; private set; }

      public IChanReceiver<Message> BsReceiver { get; private set; }

      //meta data: address used to connect
      public string MetaAddr { get; set; }

      public ChanEvent<Message> MetaEvent { get; set; }

      public ConnectionChans Free(ChanStore store, Connector conn) {
        //BsReceiver.Close().PipeEx(conn, "closing receiver (" + MetaAddr + ")");
        store.Free(BsSender);
        store.Free(BsReceiver);
        return new ConnectionChans{ };
      }

      public static async Task<ConnectionChans> Connect(Settings settings, ChanStore store, Func<string, Uri> p) {
        var broadcast = p(settings.ChanBroadcastName); //channel name
        var rT = store.GetReceiverAsync<Message>(broadcast);
        var sT = store.GetSenderAsync<Message>(broadcast);
        var k = Chan.Chan.Combine(await rT, await sT);
        return new ConnectionChans{ Broadcast = k, BsSender = await sT, BsReceiver = await rT };
      }
    }
  }
}

