using System;
using System.Threading.Tasks;

namespace Channels
{
  public interface IChan<TMsg> {
    Task Close();
  }

  public interface IChanSender<TMsg> : IChan<TMsg> {
    /// <summary>
    /// This method BLOCKS until message is enqueued/processed in LOCAL channel.
    /// It is asynchronous for NETWORK channels.
    /// </summary>
    /// <returns>Canceled: if channel is closed; Ex if cannot be put to channel for any other reason. Normal on success.</returns>
    /// <param name="msg">the message to send</param>
    Task SendAsync(TMsg msg);
  }
  //first to ask gets the message
  public interface IChanReceiver <TMsg> : IChan<TMsg> {
    /// Returned task is cancelled if the channel is closed and there will be no more messages.
    Task<TMsg> ReceiveAsync();
  }

  public interface IChanBoth<TMsg> : IChanSender<TMsg>, IChanReceiver<TMsg> {

  }
}

