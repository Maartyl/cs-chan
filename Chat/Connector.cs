using System;
using System.Threading.Tasks;

namespace Chat
{
  public class Connector : DispatchDict<string, CmdArg> {

    protected override void Default(string cmd, CmdArg arg) {
      var args = arg.Text == null ? "" : " // " + arg;
      RunError("Command not found: " + cmd + args);
    }

    public bool RunError(CmdArg arg) {
      return Run(Cmd.NotifyError, arg);
    }

    public bool RunNotifySystem(CmdArg arg) {
      return Run(Cmd.NotifySystem, arg);
    }

    public void Reregister(string command, Action<CmdArg> handler) {
      Register(command, handler, RegisterOpts.Replace);
    }

    public void Coregister(string command, Action<CmdArg> handler) {
      Register(command, handler, RegisterOpts.Merge);
    }

    public bool Run(string command, CmdArg arg, bool notifyOnNotFound) {
      return TryRun(command, arg, notifyOnNotFound, true);
    }

    public bool Run(string command) {
      return TryRun(command, default(CmdArg), true, true);
    }

    public bool RunOrDefault(string command) {
      return RunOrDefault(command, default(CmdArg));
    }

    private bool TryRun(string command, CmdArg arg = default(CmdArg), bool alsoNotifyOnNotFound = true, bool alsoThrow = false) {
      RunErrOpts opts = alsoNotifyOnNotFound ? RunErrOpts.Defaults : RunErrOpts.InformsOnly;
      if (alsoThrow)
        opts |= RunErrOpts.Throws;
      return Run(command, arg, opts);
    }

    public void PipeEx(Exception ex, string source) {
      RunError(
#if DEBUG
        ex.ToString() 
#else
        ex.Message 
#endif
        .ArgSrc(source));
    }

    public void PipeEx(Exception ex) {
      PipeEx(ex, null);
    }

    public Task PipeEx(string source, Task t1, Action<Exception> alsoDo) {
      return t1.ContinueWith(t => {
        PipeEx(t.Exception, source);
        alsoDo(t.Exception);
      }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public Task PipeEx(string source, Task t1) { 
      return PipeEx(source, t1, _ => {
      });
    }

    public Task PipeEx(Task t1) {
      return PipeEx(null, t1);
    }
  }
}

