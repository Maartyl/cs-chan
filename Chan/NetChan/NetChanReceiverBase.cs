using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanReceiverBase<T> : NetChanTBase<T>, IChanReceiver<T> {
    //of Task, so I can propagate exceptions
    IChan<Task<T>> world = new ChanAsync<Task<T>>();

    protected NetChanReceiverBase(NetChanConfig<T> cfg):base(cfg) {

    }

    protected async Task StartReceiver() {
      //... only responds; no other autonom action (like sender)
      await ReceiveLoop();
      await Close();
    }

    protected override async Task<Header> OnMsgReceived(Header h) {
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

        //deserialize data nad send to world as OK task
        var ms = new MemoryStream(bfr, 0, msgSize);
        var msg = SerDes.Deserialize(ms);
        await world.SendAsync(Task.FromResult(msg));
        await SendSimple(Header.AckFor(h)); //inform sender that msg has been received fine
        await Flush();
      } catch (Exception ex) {
        failed = ex;
      }
      if (failed != null) {
        var tc = new TaskCompletionSource<T>();
//        if (failed is TaskCanceledException) tc.SetCanceled(); else
        tc.SetException(failed);
        await world.SendAsync(tc.Task);
        throw failed;
      }
      if (receiveBuffer.Length < bfr.Length)
        receiveBuffer = bfr;
      return await hNext; //will never get here if failed; otherwise !=null
    }

    protected override async Task OnCloseReceived(Header h) {
      await world.Close();
      RequestCancel();
    }

    public async Task<T> ReceiveAsync() {
      return await await world.ReceiveAsync();
    }

    protected override async Task CloseOnce() {
      await SendSimple(Header.Close);
      await Flush();
    }
  }
}

