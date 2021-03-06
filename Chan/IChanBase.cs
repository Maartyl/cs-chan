using System;
using System.Threading.Tasks;

namespace Chan
{
  public interface IChanBase {
    Task Close();

    ///completes at the same time as Close() but does not initiate closing.
    /// - any subsequent call to receive or send is bound to be cancelled
    Task AfterClosed();
  }

  public interface IChanSender<in TMsg> : IChanBase {
    /// <summary>
    /// Completes when: (local: received / enqueued) (net: sent fine)
    /// </summary>
    /// <returns>Canceled: if channel is closed; Ex if cannot be put to channel for any other reason. Normal on success.</returns>
    /// <param name="msg">the message to send</param>
    Task SendAsync(TMsg msg);
  }
  //first to ask gets the message
  public interface IChanReceiver <TMsg> : IChanBase {
    /// Returned task is cancelled if the channel is closed and there will be no more messages.
    Task<TMsg> ReceiveAsync();
    //this effectively allows piping
    Task<TMsg> ReceiveAsync(Func<TMsg, Task> sendResult);
  }

  public interface IChan<TMsg> : IChanSender<TMsg>, IChanReceiver<TMsg> {

  }
}

