using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chat
{
  public abstract class Dispatch<TCmd, TArg> {

    protected abstract Action<TArg> Select(TCmd cmd);

    /// action also takes not found command
    protected abstract void Default(TCmd cmd, TArg arg);

    protected abstract void RegisterImpl(TCmd cmd, Action<TArg> handler, RegisterOpts opts);

    public void Register(TCmd cmd, Action<TArg> handler, RegisterOpts opts) {
      RegisterImpl(cmd, handler, opts);
    }

    public void Register(TCmd cmd, Action<TArg> handler) {
      Register(cmd, handler, RegisterOpts.Add);
    }

    public void Alias(TCmd command, TCmd aliasTo, RunErrOpts erropts = RunErrOpts.Defaults, RegisterOpts opts = RegisterOpts.Add) {
      //far from perfect, but simple solution
      Register(command, a => Run(aliasTo, a, erropts), opts);
    }

    public bool Run(TCmd cmd, TArg arg, RunErrOpts opts = RunErrOpts.Throws) {
      return CoreRun(cmd, arg, opts, CoreInvokeSynchronously).Result;
    }

    public Task<bool> RunAsync(TCmd cmd, TArg arg, RunErrOpts opts = RunErrOpts.Throws) {
      return CoreRun(cmd, arg, opts, CoreInvokeAsynchronously);
    }

    private async Task<bool> CoreRun(TCmd cmd, TArg arg, RunErrOpts opts, Func<Action<TArg>, TArg, Task> invoker) {
      var a = Select(cmd);
      if (a != null) {
        await invoker(a, arg);
        return true;
      }
      if ((opts&RunErrOpts.Defaults) != default(RunErrOpts))
        Default(cmd, arg);
      if ((opts&RunErrOpts.Throws) != default(RunErrOpts))
        throw new NoSuchCmdException(cmd, arg);
      return false;
    }

    private static Task CoreInvokeSynchronously(Action<TArg>a, TArg arg) {
      a(arg);
      return Task.WhenAll();
    }

    private static Task CoreInvokeAsynchronously(Action<TArg>a, TArg arg) {
      return Task.Run(() => a(arg));
    }

    public bool RunOrDefault(TCmd cmd, TArg arg) {
      return Run(cmd, arg, RunErrOpts.Defaults);
    }

    public Task<bool> RunOrDefaultAsync(TCmd cmd, TArg arg) {
      return RunAsync(cmd, arg, RunErrOpts.Defaults);
    }

    public enum RegisterOpts {
      Add,
      Replace,
      Merge
    }

    [Flags] public enum RunErrOpts {
      InformsOnly = 0,
      Defaults = 1,
      Throws = 2
    }

    public class NoSuchCmdException : KeyNotFoundException {
      public TCmd Cmd { get; private set; }

      public TArg Arg { get; private set; }

      public NoSuchCmdException(TCmd cmd, TArg arg) : base("Command not found: " + cmd) {
        this.Cmd = cmd;
        this.Arg = arg;
      }
    }
  }

  public abstract class DispatchDict<TCmd, TArg> : Dispatch<TCmd, TArg> {
    readonly Dictionary<TCmd, Action<TArg>> commands = new Dictionary<TCmd, Action<TArg>>();

    public Dictionary<TCmd, Action<TArg>>.KeyCollection Keys{ get { return commands.Keys; } }

    protected sealed override Action<TArg> Select(TCmd cmd) {
      Action<TArg> a;
      return (commands.TryGetValue(cmd, out a)) ? a : null;
    }

    protected sealed override void RegisterImpl(TCmd cmd, Action<TArg> handler, RegisterOpts opts) {
      if (opts == RegisterOpts.Replace) {
        lock (commands) {
          if (handler == null)
            commands.Remove(cmd);
          else
            commands[cmd] = handler;
        }
        return;
      }
      lock (commands) {
        var a = Select(cmd);
        if (a != null && opts == RegisterOpts.Add)
          throw new ArgumentException("Command already registered:" + cmd);
        RegisterImpl(cmd, a + handler, RegisterOpts.Replace);
      }
    }
  }

  public class DispatchWithDefault<TCmd, TArg> : DispatchDict<TCmd, TArg> {
    readonly Action<TCmd, TArg> onDefault;

    public DispatchWithDefault(Action<TCmd, TArg> onDefault) : base() {
      this.onDefault = onDefault;
    }

    protected sealed override void Default(TCmd cmd, TArg arg) {
      onDefault(cmd, arg);
    }
  }
}

