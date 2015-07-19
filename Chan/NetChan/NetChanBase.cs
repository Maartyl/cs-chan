using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  //current limitation: most is done asynchronously, but sequentially
  // - i'm not sure about the implications of doing it concurrently... 
  // - - (probably need swaping read bufferes / ...) - well, maybe not if I only did 1 packet at a time... 
  // - - maybe; not priority
  // - - - ReadHeader could start reading body as soon as done, knowing for which ops...
  public abstract class NetChanBase {
    const int MINIMAL_PING_DELAY = 50;
    readonly Stream netIn;
    readonly Stream netOut;
    protected byte[] receiveBuffer;
    protected byte[] sendBuffer;
    int pingDelayMs;
    volatile int positionCounter = 1;
    //changed to true when PONG packet received; =false before sending PING
    volatile bool pongReceived = true;
    //this cancels all that needs it at end: ping loop, receiveing...
    readonly CancellationTokenSource cancelSource = new CancellationTokenSource();

    protected NetChanBase(NetChanConfig cfg) {
      //a thought about defaults: 1024,2048,60*1000
      this.netIn = cfg.In;
      this.netOut = cfg.Out;
      this.receiveBuffer = new byte[cfg.InitialReceiveBufferSize];
      this.sendBuffer = new byte[cfg.InitialSendBufferSize];
      this.pingDelayMs = cfg.PingDelayMs < MINIMAL_PING_DELAY ? MINIMAL_PING_DELAY : cfg.PingDelayMs;
    }

    protected async Task HandshakeServer(uint key) {
      DbgCns.Trace(this, "handshake-srv0");
      var h = await ReceiveHeader();
      if (h.OpCode == Header.Op.Open) {
        if (h.Key != key) {
          DbgCns.Trace(this, "handshake-srv-wrong-key", key + "/" + h.Key);
          await SendError("wrong key");
          //TODO: decide: safe to put keys in err msg? - it's just something random...
          throw new AccessViolationException("expected and provided key don't match (Expected: " + key + ", Got:" + h.Key + ")");
        }
        await SendSimple(Header.AckFor(h));
        await Flush();
        DbgCns.Trace(this, "handshake-srv1");
      } else {
        DbgCns.Trace(this, "handshake-srv-EX", "" + h.OpCode);
        await SendError("expected OPEN");
        throw new InvalidOperationException("expected OPEN; got: " + h.OpCode);
      }
    }

    protected async Task HandshakeClient(uint key) {
      DbgCns.Trace(this, "handshake-clnt0");
      await SendSimple(new Header(Header.Op.Open) { Key=key });
      await Flush();
      var hResponse = await ReceiveHeader();
      DbgCns.Trace(this, "handshake-clnt1");

      if (hResponse.OpCode == Header.Op.Ack) 
        return;
      if (hResponse.OpCode == Header.Op.Err) 
        await OnErrReceived(hResponse);
      else
        throw new InvalidOperationException("expected ACK/ERR; got: " + hResponse.OpCode);
    }

    public abstract Task Start(uint key);

    /// On*Received hooks return next header
    /// (so they can start receivening as soon as possible (which might not be right away: has data))
    protected async Task ReceiveLoop() {
      try {
        var h = await ReceiveHeader();
        while (true)
          switch (h.OpCode) {
            case Header.Op.Msg:
              h = await OnMsgReceived(h);
            break;
            case Header.Op.Ping:
              h = await OnPingReceived(h);
            break;
            case Header.Op.Pong:
              h = await OnPongReceived(h);
            break;
            case Header.Op.Ack:
              h = await OnAckReceived(h);
            break;
            case Header.Op.Open:
              h = await OnOpenReceived(h);
            break;
            case Header.Op.Close:
              await OnCloseReceived(h);
              DbgCns.Trace(this, "r-loop-close");
              return;
            case Header.Op.Err:
              await OnErrReceived(h);
              DbgCns.Trace(this, "r-loop-err");
              return;
            default:
              h = await OnDefaultReceived(h);
            break;
          //TODO: add OnEndReceived
          }
      } catch (TaskCanceledException) {
        DbgCns.Trace(this, "r-loop-cancel");
        //not sure how to return something cancelled...
        // - maybe not catch?
        // - Do I want that? Or do I want to end fine?
        // - Has canceled receiving ended fine?
      }

    }

    protected abstract Task<Header> OnMsgReceived(Header h);

    protected async Task<Header> OnPingReceived(Header h) {
      DbgCns.Trace(this, "on-ping");
      var hNext = ReceiveHeader();
      await SendSimple(Header.Pong);
      await Flush();
      return await hNext;
    }

    protected Task<Header> OnPongReceived(Header h) {
      DbgCns.Trace(this, "on-pong");
      pongReceived = true;
      return ReceiveHeader();
    }

    protected virtual Task<Header> OnAckReceived(Header h) {
      //basic implementation does not require MSG.ACK: just ignore...
      return ReceiveHeader();
    }

    protected async Task<Header> OnOpenReceived(Header h) {
      //initial OPEN can is handled by something else: this is just error in normal listen...
      // ... or is it? can it be used for anything?
      await SendError("unexpected: OPEN");
      throw new InvalidOperationException("received OPEN in already opened connection");
    }

    protected virtual Task<Header> OnDefaultReceived(Header h) {
      throw new InvalidOperationException("unknown header opcode: " + h.OpCode);
    }

    protected abstract Task OnCloseReceived(Header h);

    protected async Task OnErrReceived(Header h) {
      DbgCns.Trace(this, "on-err0");
      var msgLen = h.Length;
      if (receiveBuffer.Length < msgLen)
        receiveBuffer = new byte[msgLen];
      var buff = receiveBuffer; //retain reference if cleanup frees buffers
      var msgT = ReceiveBytes(buff, 0, msgLen, "ERR message");

      //TODO: perform cleanup (cancel ping, delete stuff, call something virtual, ...)
      RequestCancel();
      //sendBuffer = null;// possibly GC sooner - reference to this object might still exist for quite a while
      //receiveBuffer = null;

      await msgT;
      var msgStr = System.Text.Encoding.UTF8.GetString(buff, 0, msgLen);
      DbgCns.Trace(this, "on-err1", msgStr);
      throw new RemoteException(msgStr);
    }

    protected Task SendSimple(Header h) {
      return netOut.WriteAsync(h.Bytes, 0, Header.Size, Token);
    }

    protected Task SendBytes(Header h, byte[] bytes, int offset, ushort count) {
      h.Length = count;
      if (count == 0)
        return SendSimple(h);

      var packetSize = count + Header.Size;
      if (sendBuffer.Length < packetSize)
        sendBuffer = new byte[packetSize];

      var buff = sendBuffer;
      //merge into 1 write: ~ping/something could be sent in midde, breaking the packet
      // [h] + [bbbbb] -> [hbbbbb]
      Array.Copy(h.Bytes, buff, Header.Size);
      if (buff != bytes || offset != Header.Size)//only copy if not the same place already (should be the same often)
        Array.Copy(bytes, offset, buff, Header.Size, count);
      return netOut.WriteAsync(buff, 0, packetSize, Token);
    }

    protected async Task SendError(ushort code, string message) {
      var bs = System.Text.Encoding.UTF8.GetBytes(message);
      if (bs.Length > ushort.MaxValue)
        throw new ArgumentException("error message too long (max: 64KB)", "message");
      await SendBytes(new Header(Header.Op.Err) { ErrorCode = code }, 
                      bs, 0, (ushort) bs.Length);
      await Flush();
    }

    protected Task SendError(string message) {
      return SendError(0, message);
    }

    protected Header CreateBaseMsgHeader() {
      #pragma warning disable 420
      var pos = Interlocked.Increment(ref positionCounter);
      #pragma warning restore 420
      var h = new Header(Header.Op.Msg);
      h.Position = (ushort) (pos % (ushort.MaxValue + 1));
      return h;
    }

    protected async Task<Header> ReceiveHeader() {
      var bs = new byte[8];
      await ReceiveBytes(bs, 0, 8, "packet header");
      return new Header(bs);
    }
    //continuously sends pings
    protected async Task PingLoop(CancellationToken ctkn) {
      DbgCns.Trace(this, "ping-loop0");
      try {
        await Task.Delay(pingDelayMs, ctkn);
        while (pongReceived) {
          pongReceived = false;
          DbgCns.Trace(this, "ping-loop");

          var delay = Task.Delay(pingDelayMs, ctkn);
          await SendSimple(Header.Ping);
          await Flush();
          await delay;
        }
      } catch (TaskCanceledException) {
        DbgCns.Trace(this, "ping-loop-end-ok");
        return; //ok end
      }
      RequestCancel();
      DbgCns.Trace(this, "ping-loop-timeout");
      throw new TimeoutException("PING: no PONG received");
    }

    ///NetworkStream.Flush does nothing
    /// - this can be useful in case, when NetworkStream is wrapped / something else entirely ...
    protected Task Flush() {
      return netOut.FlushAsync(); //shouldn't need to be cancellable
    }

    protected void RequestCancel() {
      cancelSource.Cancel();
    }

    protected CancellationToken Token { get { return cancelSource.Token; } }

    /// doesn't do "fit/null" checks
    protected async Task ReceiveBytes(byte[] buffer, int index, int count, string errWhatReceiving) {
      int read = 0; //#of already read bytes == pos to bs where to read to
      while (read != count) {
        var curRead = await netIn.ReadAsync(buffer, index + read, count - read, Token);
        if (curRead <= 0)
          throw new EndOfStreamException("EOS while receiving " + errWhatReceiving + "; read: (" + read + "/" + count + ")");
        else
          read += curRead;
        //TODO: ?: start deserializing here
      }
    }
    //this class is not thread safe
    protected class Header {
      readonly byte[] data;

      public Header(byte[] data) {
        this.data = data;
      }

      public Header(Op opcode) {
        data = new byte[8];
        OpCode = opcode;
      }

      public static int Size{ get { return 8; } }

      public byte[] Bytes{ get { return data; } }

      public Op OpCode{ get { return (Op) data[0]; } set { data[0] = (byte) value; } }
      //cannot use BitConverter to ensure endianness
      public ushort Fragment {
        get { return (ushort) (data[4] * 256 + data[5]);}
        set {
          data[5] = (byte) (value&255);
          data[4] = (byte) ((value >> 8)&255);
        }
      }

      public ushort Length {
        get { return (ushort) (data[6] * 256 + data[7]);} 
        set {
          data[7] = (byte) (value&255);
          data[6] = (byte) ((value >> 8)&255);
        }
      }

      public ushort Position {
        get { return (ushort) (data[2] * 256 + data[3]);}
        set {
          data[3] = (byte) (value&255);
          data[2] = (byte) ((value >> 8)&255);
        }
      }

      public ushort ErrorCode { get { return Fragment; } set { Fragment = value; } }

      public uint Key {
        get{ return Fragment * 256u * 256 + Length;}
        set {
          var shortMask = 256 * 256 - 1;
          Fragment = (ushort) ((value >> 16)&shortMask);
          Length = (ushort) (value&shortMask);
        }
      }

      ///true == not last fragment of message
      public bool HasNextFragment {
        get { return GetFlagValue(Flag.HasNext); }
        set { SetFlagValue(Flag.HasNext, value); }
      }

      bool GetFlagValue(Flag f) {
        return (data[1]&(int) f) != 0;
      }

      void SetFlagValue(Flag f, bool value) {
        if (value) 
          data[1] |= (byte) f;
        else 
          data[1] &= (byte) ~(byte) f;
      }

      public static Header AckFor(Header h) {
        var hAck = new Header(Op.Ack);
        hAck.Position = h.Position;
        hAck.Fragment = h.Fragment;
        return hAck;
      }

      public override string ToString() {
        string format = "[{0} {1}]";
        if (OpCode == Op.Err)
          format = "[{0} {1} {5}#{3}]";
        else if (OpCode == Op.Msg)
            format = "[{0} {1} {2}{7}#{3}]";
          else if (OpCode == Op.Open) 
              format = "[{0} {1} {6}]";
        string hex = BitConverter.ToString(Bytes).Replace("-", "");
        return string.Format(format, hex, OpCode, Fragment, Length, Position, ErrorCode, Key, HasNextFragment ? "+" : "");
      }

      public static readonly Header Ping = new Header(Op.Ping);
      //without same position: this Pong is enough
      public static readonly Header Pong = new Header(Op.Pong);
      public static readonly Header Close = new Header(Op.Close);

      [Flags]
      private enum Flag : byte {
        HasNext = 1
      }

      public enum Op : byte {
        //only 0s will be interpreted as err with no message
        Err = 0,
        Msg = 1,
        Ack = 2,
        Open = 100,
        //End = 102,
        Close = 110,
        Ping = 200,
        Pong = 201
        //future?: possibly add "I want to receive" for better workload distribution
        // - the ones with more "I want to receive" would get sent more messages
        // ... this is 2 way communication, though...
      }
    }
  }
}

