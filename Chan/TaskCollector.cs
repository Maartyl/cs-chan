using System;
using System.Threading.Tasks;

namespace Chan
{
  ///either Fails when First collected task fails
  ///OR(default:) when all tasks end, merging exceptions in a singly-linked list manner
  /// - if all ok: completes after everything before .Close() completes
  public class TaskCollector {
    //this is set when whole thing should end
    readonly ExceptionDrain drain = new ExceptionDrain();
    readonly ChanAsync<Task> queue = new ChanAsync<Task>();
    readonly Task finalTask;
    readonly bool prematureCompletion;

    public TaskCollector(bool prematureCompletion) {
      finalTask = CollectTasks();
      this.prematureCompletion = prematureCompletion;
    }

    public TaskCollector() : this(false) {
    }

    async Task CollectTasks() {
      var t = await queue.ReceiveAsync();
      if (t == null) {
        drain.EndOk();
        return;
      }
      if (prematureCompletion) {
        await t;
        await CollectTasks();
      } else
        await Task.WhenAll(t, CollectTasks());
    }

    void AddImpl(Task t) {
      drain.Consume(
        queue.SendAsync(t), registerCancel: true); //I'm aware of ignoring task: only err: not added: ok => discard
    }

    ///Any Tasks added after .Close() will be discarded
    ///<returns>probably Task accepted (i.e. not closed) - not thread safe</returns>
    public bool Add(Task t) {
      if (t == null)
        throw new ArgumentNullException("t");
      if (queue.Closed)
        return false;
      AddImpl(t);
      return true;
    }

    public void Close() {
      AddImpl(null);
      drain.Consume(
        queue.Close()); //no exception, only when all done: I don't care here
    }

    public Task Task{ get { return finalTask; } }

    public Task AddingTask{ get { return drain.Task; } }
  }
}

