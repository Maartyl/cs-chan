using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Chat
{
  //I thougt I made something cool, but this is essentailly just TaskCompletionsSource ...
  // - and it doesn't even work that well...

  //this will allow me to add reply to CmdArg (or something third that is not serializable...)

  //also: cannot reply multiple times: would be only run n times with the LAST value
  // - so I made it imposible to run any continuation twice...
  // - it would be cooler(?) to run it every time but M$ likes its mutable weid stuff...
  // - because Result is set as instance var and then read, it has to be the same...
  public class ActionTAwaiter<T> : INotifyCompletion {
    readonly Action<T> action;
    //for event
    readonly object completedLock = new object();
    //this is added if not completed but OnCompleted called
    //called inside returned Action
    Action onCompletedEvt = null;
    T result;
    volatile bool isCompleted = false;

    public ActionTAwaiter() {
      action = arg => {
        SetResult(arg);
        InvokeOnceOnCompleted();
      };
    }

    void InvokeOnceOnCompleted() {
      Action e;
      lock (completedLock) {
        e = onCompletedEvt;
        onCompletedEvt = null;
      }
      if (e != null) {
        e();
        InvokeOnceOnCompleted(); //retry if something added meanwhile until  (==null)
      } else return;
    }

    public Action<T> Action { get { return action; } }

    public ActionTAwaiter<T> GetAwaiter() {
      return this;
    }

    public bool IsCompleted { get { return isCompleted; } }

    void SetResult(T r) {
      result = r;
      isCompleted = true;
    }

    public T GetResult() {
      if (IsCompleted)
        return result;
      else {
        var ts = new TaskCompletionSource<T>();
        OnCompleted(ts.SetResult);
        return ts.Task.Result; //wait for result - not nice but no other option
      }
    }
    //just nicer
    public void OnCompleted(Action<T> continuation) {
      OnCompleted(() => continuation(GetResult()));
    }

    public void OnCompleted(Action continuation) {
      if (!IsCompleted)
        lock (completedLock)
          if (!IsCompleted) {
            onCompletedEvt += continuation;
            return;
          }
      //is completed already
      continuation();
    }
  }

  public static class TestAwaiter {

    struct Arg {
      public string Text { get; set; }

      public Action<string> Reply { get; set; }
    }

    class Connector : DispatchDict<string, Arg> {
      protected override void Default(string cmd, Arg arg) {
        throw new Exception("not found key: " + cmd);
      }
    }

    static ActionTAwaiter<string> run(string cmd, string text, Connector conn) {
      var aw = new ActionTAwaiter<string>();
      //I tried capturing current context but it acts the same
      var cxt = SynchronizationContext.Current;
      Action<string> a = cxt == null ? aw.Action : k => cxt.Send(_ => aw.Action(k), null);
      var arg = new Arg{ Text = text, Reply = a };
      conn.Run(cmd, arg);
      return aw;

//      var tcs = new TaskCompletionSource<string>();
//      var arg = new Arg{ Text = text, Reply = tcs.SetResult };
//      conn.Run(cmd, arg);
//      return tcs.Task;
    }

    static void writeln(string str) {
      var id = Thread.CurrentThread.ManagedThreadId;
      Console.WriteLine("{0:3}@ {1}", id, str);
    }


    public static void T() {
      var conn = new Connector();
      conn.Register("ignore", a => writeln("ignore.0"));

      conn.Register("reply", a => {
        writeln("reply.0");
        a.Reply("reply from reply");
        writeln("reply.1");
      });

      conn.Register("react", a => {
        writeln("react.0");
        a.Reply("reply from react: " + a.Text);
        writeln("react.1");
      });

      conn.Register("twice", a => {
        writeln("twice.0");
        a.Reply("reply from twice: " + "<first>" + " " + a.Text);
        writeln("twice.1");
        a.Reply("reply from twice: " + "<second>" + " " + a.Text);
        writeln("twice.1");
      });

      writeln("A.{");
      var aT = A(conn);
      writeln("A.}");

      writeln("B.{");
      var bT = B(conn);
      writeln("B.}");

      writeln("C.{");
      var cT = C(conn);
      writeln("C.}");

      writeln("D.{");
      var dT = D(conn);
      writeln("D.}");

      writeln("E.{");
      var eT = E(conn);
      writeln("E.}");

      Task.WhenAll(aT, bT, cT, dT, eT).Wait(); //aT should cause it never finishes
    }

    static async Task A(Connector conn) {
      writeln("A.0");
      var t = await run("ignore", "a text", conn);
      writeln("I DONT EXPECT TO EVER SEE THIS");
      writeln("A.1: " + t);
    }

    static async Task B(Connector conn) {
      writeln("B.0");
      var t = await run("reply", "b text", conn);
      writeln("B.1: " + t);
    }

    static async Task C(Connector conn) {
      writeln("C.0");
      var t = await run("react", "c text", conn);
      writeln("C.1: " + t);
    }

    static async Task D(Connector conn) {
      writeln("D.0");
      var t = await run("twice", "d text", conn);
      writeln("D.1: " + t);
    }

    static async Task E(Connector conn) {
      writeln("E.0");
      var t1 = await run("react", "[e text 1]", conn);
      writeln("E.1: " + t1);
      var t2 = await run("react", "[e text 2]", conn);
      writeln("E.2: " + t2);
    }
  }
}

