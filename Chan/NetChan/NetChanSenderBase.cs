using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanSenderBase<T> : NetChanTBase<T>, IChanSender<T> {
    readonly IChan<DataWithErrorInfo> world = new ChanAsync<DataWithErrorInfo>();

    protected NetChanSenderBase(NetChanConfig<T> cfg) : base(cfg) {

    }

    protected async Task StartSender() {
      DbgCns.Trace(this, "start-sender0");
      var receiverT = ReceiveLoop();
      var senderT = PipeWorldSend();
      Exception failed = null;
      try {
        /*var firstT =*/
        await Task.WhenAny(receiverT, senderT);
        //if senderT is first (channel got closed): there still could be exception in receiver / could go on forever...
        // - cancellation token? - crazy distribution / inst.member
        DbgCns.Trace(this, "start-sender1");
      } catch (Exception ex) {
        failed = ex;
        DbgCns.Trace(this, "start-sender-EX");
      }
      if (failed != null) {
        //continue catch clause outside of it: cannot contain await (sadly without rethrow; makes sense, though)
        await CancelWorld(failed);
        throw failed;
      }
      DbgCns.Trace(this, "start-senderE1");
      await Close();
      await Task.WhenAll(receiverT, senderT);
      DbgCns.Trace(this, "start-senderE2");
    }

    protected Task SendMsg(T msg) {
      DbgCns.Trace(this, "send0");
      ushort length;
      var buff = sendBuffer; //in case someone changes the buffer
      bool couldReuseBuffer; //thanks to this: sends from 0: SendBytes will shift the data while merging
      try {
        //try reuse buffer : should work most of the time
        //this alows me to start writing after space for header: saves me from shifting data
        var ms = new MemoryStream(buff, Header.Size, buff.Length - Header.Size);
        SerDes.Serialize(ms, msg);
        length = (ushort) ms.Position; //actually used count
        couldReuseBuffer = true;
        DebugCounter.Incg(this, "ser-fast");
        DbgCns.Trace(this, "send-fast", length.ToString());
      } catch (NotSupportedException) {
        //buffer too short: do again, able to resize and change buffer to created new: bigger
        //sadly: needs to shift: written from beginning

        //in case buffer was already maximal size and still wasn't enough
        if (buff.Length >= Header.Size + ushort.MaxValue)
          throw new NotSupportedException("messages over 64KB are not supported");

        //I know the current size was not enough: I know I can start there++ (it will be more)
        var ms = new MemoryStream(buff.Length + Header.Size);
        SerDes.Serialize(ms, msg);
        if (ms.Length > ushort.MaxValue)
          throw new NotSupportedException("messages over 64KB are not supported");
        length = (ushort) ms.Position;//actually used count (length here works the same)
        buff = ms.GetBuffer();
        couldReuseBuffer = false;
        DebugCounter.Incg(this, "ser-slow");
        DbgCns.Trace(this, "send-slow", length.ToString());
      }
      if (sendBuffer.Length < buff.Length)
        sendBuffer = buff;
      return SendBytes(CreateBaseMsgHeader(), buff, couldReuseBuffer ? Header.Size : 0, length);
    }

    protected async Task PipeWorldSend() {
      DbgCns.Trace(this, "pipe0");
      //assures there are no 2 messages being sent at the same time
      // - could happen if sending was accesible directly
      try {
        while (true) {
          var derT = world.ReceiveAsync(); 
          if (!derT.IsCompleted) //next message is not immediately available
            await Flush(); //TODO: assume Flush can fail: either just cancel cur or: list of all not flushed... 
          var der = await derT;
          DbgCns.Trace(this, "pipe1");
          try { 
            await SendMsg(der.Data);
            der.SetCompleted();
          } catch (Exception ex) { 
            DbgCns.Trace(this, "pipe-EX", ex.Message);
            der.SetException(ex);
            throw; //should I kill the whole thing? ... probably
          }
        }
      } catch (TaskCanceledException) {
        DbgCns.Trace(this, "pipeE0");
        //ok done: send close
        //any other exception will propagate: not calling CLOSE
      }
      await SendSimple(Header.Close);
      await Flush();
      DbgCns.Trace(this, "pipeE");
    }

    protected async Task CancelWorld(Exception ex) {
      //exception happened: all senders fail
      DbgCns.Trace(this, "cw0");
      var endT = world.Close();
      try {
        while (true)
          (await world.ReceiveAsync()).SetException(ex);
      } catch (TaskCanceledException) {
        //end of all senders: world is closed: no more can appear
      }
      await endT;
      DbgCns.Trace(this, "cwE");
    }

    #region implemented abstract members of NetChanBase

    protected override Task<Header> OnMsgReceived(Header h) {
      DbgCns.Trace(this, "on-msg");
      throw new InvalidOperationException("MSG received in Sender");
    }

    protected override async Task OnCloseReceived(Header h) {
      DbgCns.Trace(this, "on-close");
      //receiver requested no more messages
      //-> close from outside: when all done, send CLOSE anyway (actual close: receiver doesn't know when end)

      //finishes "listening" after done.
      // -> close cannot wait for finished listening...
      // --> there should be 2 levels of close
      await world.Close(); // this completes at the ~same time as PipeWorldSend
      //now: only somehow inform the whole thing that we are closed...
      // - world already knows and cannot cause problems (people only see SendAsync: that returns Cancel)
      // solved in StartSender : when world closes: so will PipeWorldSender (and this: receiver)
      // after that: StartSender calls Close on this.
    }

    #endregion

    public async Task SendAsync(T msg) {
      //this task finshes when message has been succesfully sent: not received
      DbgCns.Trace(this, "senda0");
      var der = new DataWithErrorInfo(msg);
      await world.SendAsync(der);
      DbgCns.Trace(this, "senda1");
      await der.Task;
      DbgCns.Trace(this, "sendaE");
      //whenAll would never finish if world.send got canceled
    }

    protected override async Task CloseOnce() {
      DbgCns.Trace(this, "close0");
      await world.Close();
      RequestCancel();
      DbgCns.Trace(this, "closeE");
      //close world; call virtual cleanup
      //DON'T send CLOSE : it will be called once PipeWorlSend completes: after read all from world
      // ... this is probably really all I need...
      // maybe call some cleanup but that can be done in some lower level
    }

    ///backwards propagation of exceptions
    private class DataWithErrorInfo : TaskCompletionSourceEmpty {
      public T Data { get; private set; }

      public DataWithErrorInfo(T data) {
        Data = data;
      }
    }
  }
}

