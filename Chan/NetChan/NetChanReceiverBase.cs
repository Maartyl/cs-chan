using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanReceiverBase<T> : NetChanTBase<T>, IChanReceiver<T> {
    //of Task, so I can propagate exceptions
    IChan<Task<T>> world = new ChanAsync<Task<T>>();

    protected NetChanReceiverBase(NetChanConfig<T> cfg) : base(cfg) {

    }

    protected async Task StartReceiver() {
      //... only responds; no other autonom action (like sender)
      DbgCns.Trace(this, "start-rec0");
      await ReceiveLoop();
      DbgCns.Trace(this, "start-rec1");
      await Close();
      DbgCns.Trace(this, "start-recE");
    }

    protected override async Task<Header> OnMsgReceived(Header h) {
      DbgCns.Trace(this, "on-msg0");
      if (h.HasNextFragment)
        throw new NotSupportedException("fragmented messages (>64KB) are not suppoed");
      var msgSize = h.Length;
      var bfr = receiveBuffer;
      if (bfr.Length < msgSize)
        bfr = new byte[msgSize];
      Task<Header> hNext = null;
      Exception failed = null;
      try { //receive data: pipe exception to receiver and then throw (will stop ReceiveLoop)
        await ReceiveBytes(bfr, 0, msgSize, "message data");
        hNext = ReceiveHeader(); //TODO: (only speed) make receive data of packet asynchronously as well

        DbgCns.Trace(this, "on-msg-awaitedK");

        //deserialize data nad send to world as OK task
        var ms = new MemoryStream(bfr, 0, msgSize);
        var msg = SerDes.Deserialize(ms);
        DbgCns.Trace(this, "on-msg-deserK");
        await world.SendAsync(Task.FromResult(msg));
        DbgCns.Trace(this, "on-msg-worldK");
        await SendSimple(Header.AckFor(h)); //inform sender that msg has been received fine
        await Flush();
        DbgCns.Trace(this, "on-msg-ack-sent");
      } catch (Exception ex) {
        DbgCns.Trace(this, "on-msg-EX", ex.Message);
        failed = ex;
      }
      DbgCns.Trace(this, "on-msg3-after-try");
      if (failed != null) {
        var tc = new TaskCompletionSource<T>();
//        if (failed is TaskCanceledException) tc.SetCanceled(); else
        tc.SetException(failed);
        await SendError(failed.ToString());
        await world.SendAsync(tc.Task);
        throw failed;
      }
      if (receiveBuffer.Length < bfr.Length)
        receiveBuffer = bfr;
      return await hNext; //will never get here if failed; otherwise !=null
    }

    protected override async Task OnCloseReceived(Header h) {
      DbgCns.Trace(this, "on-close");
      await world.Close();
      RequestCancel();
    }

    public Task<T> ReceiveAsync() {
      DbgCns.Trace(this, "ReceiveAsync");
      return world.ReceiveAsync().Flatten();
    }

    public Task<T> ReceiveAsync(Func<T, Task> sendResult) {
      DbgCns.Trace(this, "ReceiveAsync2");
      return world.ReceiveAsync(t => t.Bind(sendResult)).Flatten();
    }

    protected override async Task CloseOnce() {
      DbgCns.Trace(this, "close-once0");
      await SendSimple(Header.Close);
      await Flush();
      DbgCns.Trace(this, "close-onceE");
    }
  }
}

