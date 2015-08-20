using System;
using System.Threading.Tasks;

namespace Chan
{
  /// <summary>
  /// Combines receiver and sender into 1 chan.
  /// - Used from helper method that can potentially return better 'combinations'
  /// - Not(!) to be used directly
  /// </summary>
  class ChanCombiner<T> : IChan<T> {
    readonly IChanReceiver<T> r;
    readonly IChanSender<T> s;

    internal ChanCombiner(IChanReceiver<T> r, IChanSender<T> s) {
      this.r = r;
      this.s = s;
    }

    public Task<T> ReceiveAsync() {
      return r.ReceiveAsync();
    }

    public Task<T> ReceiveAsync(Func<T, Task> sendResult) {
      return r.ReceiveAsync(sendResult);
    }


    public Task SendAsync(T msg) {
      return s.SendAsync(msg);
    }


    public Task Close() {
      return Task.WhenAll(r.Close(), s.Close());
    }

    public Task AfterClosed() {
      return Task.WhenAll(r.AfterClosed(), s.AfterClosed());
    }

  }
}

