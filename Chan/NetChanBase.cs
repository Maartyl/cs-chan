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
    byte[] receiveBuffer;
    byte[] sendBuffer;
    int pingDelayMs;
    volatile int positionCounter = 1;
    //changed to true when PONG packet received; false before sending PING
    volatile bool pongReceived = true;

    protected NetChanBase(Stream netIn, Stream netOut, NetChanConfig cfg) {
      //thought about defaults: 1024,2048,60*1000
      this.netIn = netIn;
      this.netOut = netOut;
      receiveBuffer = new byte[cfg.InitialReceiveBufferSize];
      sendBuffer = new byte[cfg.InitialSendBufferSize];
      this.pingDelayMs = cfg.PingDelayMs < MINIMAL_PING_DELAY ? MINIMAL_PING_DELAY : cfg.PingDelayMs;
      //init pingDelay (possibly time to wait for pong... or just next ping checks...)
      // - this will grow if needed
    }

    /// On*Received hooks return next header
    /// (so they can start receivening as soon as possible (which might not be right away: has data))
    protected virtual async Task ReceiveLoop() {
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
            return;
          case Header.Op.Err:
            await OnErrReceived(h);
            return;
          default:
            h = await OnDefaultReceived(h);
          break;
        }
    }

    protected virtual async Task ReceiveOpen(uint key) {
      var h = await ReceiveHeader();
      if (h.OpCode == Header.Op.Open) {
        if (h.Key != key) {
          await SendError("wrong key");
          //TODO: decide: safe to put keys in err msg?
          throw new AccessViolationException("expected and provided key don't match");
        }
        await SendSimple(Header.AckFor(h));
      } else {
        await SendError("expected OPEN");
        throw new InvalidOperationException("expected OPEN; got: " + h.OpCode);
      }
    }

    protected abstract Task<Header> OnMsgReceived(Header h);

    protected virtual async Task<Header> OnPingReceived(Header h) {
      var hNext = ReceiveHeader();
      await SendSimple(Header.Pong);
      return await hNext;
    }

    protected virtual Task<Header> OnPongReceived(Header h) {
      pongReceived = true;
      return ReceiveHeader();
    }

    protected virtual Task<Header> OnAckReceived(Header h) {
      //basic implementation does not require ACK: just ignore...
      return ReceiveHeader();
    }

    protected virtual async Task<Header> OnOpenReceived(Header h) {
      //initial OPEN can is handled by something else: this is just error in normal listen...
      // ... or is it? can it be used for anything?
      await SendError("unexpected: OPEN");
      throw new InvalidOperationException("received OPEN in already opened connection");
    }

    protected virtual Task<Header> OnDefaultReceived(Header h) {
      throw new InvalidOperationException("unknown header opcode: " + h.OpCode);
    }

    protected abstract Task OnCloseReceived(Header h);

    protected virtual async Task OnErrReceived(Header h) {
      var msgLen = h.Length;
      if (receiveBuffer.Length < msgLen)
        receiveBuffer = new byte[msgLen];
      var bfr = receiveBuffer; //retain reference if cleanup frees buffers
      var msgT = ReceiveBytes(bfr, 0, msgLen, "ERR message");

      //TODO: perform cleanup (cancel ping, delete stuff, call something virtual, ...)

      await msgT;
      var msgStr = System.Text.Encoding.UTF8.GetString(bfr, 0, msgLen);
      throw new RemoteException(msgStr);
    }

    protected Task SendSimple(Header h) {
      return netOut.WriteAsync(h.Bytes, 0, Header.Size);
    }

    protected Task SendBytes(Header h, byte[] bytes, int offset, ushort count) {
      h.Length = count;
      if (sendBuffer.Length < count + Header.Size)
        sendBuffer = new byte[count + Header.Size];

      //merge into 1 write: ping could be sent in midde, breaking the packet
      Array.Copy(h.Bytes, sendBuffer, Header.Size);
      if (sendBuffer != bytes || offset != Header.Size)//only copy if not the same place already
        Array.Copy(bytes, offset, sendBuffer, Header.Size, count);
      return netOut.WriteAsync(sendBuffer, 0, count + Header.Size);
    }

    protected Task SendError(/*int code,*/ string message) {
      var bs = System.Text.Encoding.UTF8.GetBytes(message);
      if (bs.Length > ushort.MaxValue)
        throw new ArgumentException("error message too long (max: 64KB)", "message");
      var hErr = new Header(Header.Op.Err);
      return SendBytes(hErr, bs, 0, (ushort) bs.Length);
    }

    protected Header CreateBaseMsgHeader() {
      var pos = Interlocked.Increment(ref positionCounter);
      var h = new Header(Header.Op.Msg);
      h.Position = (ushort) (pos % ushort.MaxValue);
      return h;
    }

    protected async Task<Header> ReceiveHeader() {
      var bs = new byte[8];
      await ReceiveBytes(bs, 0, 8, "packet header");
      return new Header(bs);
    }
    //continuously sends pings
    protected async Task PingLoop(CancellationToken ctkn) {
      try {
        await Task.Delay(pingDelayMs, ctkn);
        while (pongReceived) {
          pongReceived = false;

          var delay = Task.Delay(pingDelayMs, ctkn);
          await SendSimple(Header.Ping);
          await delay;
        }
      } catch (TaskCanceledException) {
        return; //ok end
      }
      throw new TimeoutException("PING: no PONG received");
    }

    /// doesn't do "fit" checks
    protected async Task ReceiveBytes(byte[] buffer, int index, int count, string errWhatReceiving) {
      int read = 0; //#of already read bytes == pos to bs where to read to
      while (read != count) {
        var curRead = await netIn.ReadAsync(buffer, index + read, count - read);
        if (curRead <= 0)
          throw new EndOfStreamException("EOS while receiving " + errWhatReceiving + "; read: (" + read + "/" + count + ")");
        else
          read += curRead;
        //TODO: start deserializing here
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
        return hAck;
      }

      public static readonly Header Ping = new Header(Op.Ping);
      //without same position: this Pong is enough
      public static readonly Header Pong = new Header(Op.Pong);

      [Flags]
      private enum Flag : byte {
        HasNext = 0
      }

      public enum Op : byte {
        //only 0s will be interpreted as err with no message
        Err = 0,
        Msg = 1,
        Ack = 2,
        Open = 100,
        End = 102,
        Close = 110,
        Ping = 200,
        Pong = 201
      }
    }
  }
}

