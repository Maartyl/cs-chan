using System;
using System.Threading.Tasks;

namespace Chan
{
  public interface IChan<TMsg> {
    void Close();
  }

  public interface IChanSender<TMsg> : IChan<TMsg> {
    Task SendAsync(TMsg msg);
  }
  //first to ask gets the message
  public interface IChanReceiver <TMsg> : IChan<TMsg> {
    Task<TMsg> ReceiveAsync();
  }

  public interface IChanBoth<TMsg> : IChanSender<TMsg>, IChanReceiver<TMsg> {

  }
}

