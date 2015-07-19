using System.Threading;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Chan
{
  public class MainClass {
    public static void Main(string[] args) {
      DbgCns.Trace("start", "main");
      //var ctknSrc = new CancellationTokenSource();
      //PeriodicCheckDebugCounter(ctknSrc.Token);

      //var testNet = new NetChanTest();
      //testNet.HeaderToString();

//      var test = new ChanSimpleTest();
//      Console.WriteLine("start");
//
//      test.AllPassedAsync();
//      Console.WriteLine("all");
//      DebugCounter.Glob.Print(Console.Error);
//      DebugCounter.Glob.Clear();
//
//      test.OrderWithSingleInAndOutAsync();
//      //test.OrderWithSingleInAndOutBuffered();
//      //test.OrderWithSingleInAndOutQueued();
//      Console.WriteLine("order");
//      DebugCounter.Glob.Print(Console.Error);
//      DebugCounter.Glob.Clear();

      //var chan = new ChanAsync<String>();

      //new ChanEvent<String>(chan, Console.WriteLine);

      //DebugCounter.Glob.Print(Console.Error);
      //ctknSrc.Cancel();
      // exec(cS).Wait();

      //
      Rpl().Wait();

//      var s = new ChanStore();
//      //var a = new ChanAsync<int>(); 
//      var a = new QQQ(); 
//      s.RegisterReceiver(new Uri("chan:test"), (IChanReceiver<long>) a);
//      var r2 = s.GetReceiver<int>(new Uri("chan:test"));
     
      //DebugCounter.Glob.Print(Console.Error);
    }

    static async Task Rpl() {
      DbgCns.Trace("rpl", "0");
      var rT = new TaskCompletionSource<IChanReceiver<string>>();
      var sT = new TaskCompletionSource<IChanSender<string>>();
      var nT = InitNet(rT, sT);
      DbgCns.Trace("rpl", "1");
      new ChanEvent<String>(await rT.Task, Console.WriteLine);
      DbgCns.Trace("rpl", "2");
      await Task.WhenAll(nT, exec(await sT.Task));
      DbgCns.Trace("rpl", "E");
    }

    static async Task exec(IChanSender<String> cs) {
      DbgCns.Trace("exec", "0");
      await cs.SendAsync("Hello");
      DbgCns.Trace("exec", "1");
      await cs.SendAsync("from");
      DbgCns.Trace("exec", "2");
      await cs.SendAsync("async channel");
      string s;
      while ((s = Console.ReadLine()) != null) {
        DbgCns.Trace("exec", "kb-send", s);
        await cs.SendAsync(s);
      }
      DbgCns.Trace("exec", "C");
      await cs.Close();
      DbgCns.Trace("exec", "E0");
      try {
        await cs.SendAsync("this should not be shown");
      } catch (TaskCanceledException) {
        Console.WriteLine("correctly thrown cancelled");
      }
      DbgCns.Trace("exec", "E");
    }

    static async void PeriodicCheckDebugCounter(CancellationToken ctkn) {
      try {
        while (!ctkn.IsCancellationRequested) {
          await Task.Delay(10 * 1000, ctkn);
          DebugCounter.Glob.Print(Console.Error);
        }
      } catch (TaskCanceledException) {
        
      }
    }

    static async Task InitNet<T>(TaskCompletionSource<IChanReceiver<T>> chanR, TaskCompletionSource< IChanSender<T>> chanS) {
      DbgCns.Trace("init", "0");
      TaskCompletionSource<TcpClient> serverTcpPromise = new TaskCompletionSource<TcpClient>();
      TcpListener l = new TcpListener(IPAddress.Any, 7896);
      l.Start();
      l.BeginAcceptTcpClient(ar => {
        var c = l.EndAcceptTcpClient(ar);
        serverTcpPromise.SetResult(c);
      }, l);

      //start client
      var clientTcp = new TcpClient("localhost", 7896);
      var serverTcp = await serverTcpPromise.Task;
      l.Stop();
      DbgCns.Trace("init", "tcp-connected");
      var cfgIn = new NetChanConfig<T>() {
        In = clientTcp.GetStream(),
        Out = clientTcp.GetStream(),
        InitialReceiveBufferSize = 128, 
        InitialSendBufferSize = 128,
        PingDelayMs = 60*1000
      };
      var cfgOut = new NetChanConfig<T>() {
        In = serverTcp.GetStream(),
        Out = serverTcp.GetStream(),
        InitialReceiveBufferSize = 128,
        InitialSendBufferSize = 128,
        PingDelayMs = 60*1000
      };
      var s = new NetChanSenderServer<T>(cfgOut);
      var r = new NetChanReceiverClient<T>(cfgIn);
      chanR.SetResult(r);
      chanS.SetResult(s);
      DbgCns.Trace("init", "results");
      var sT = s.Start(42);
      var rT = r.Start(42);
      DbgCns.Trace("init", "started");
      await Task.WhenAll(sT, rT);
      DbgCns.Trace("init", "E");
    }
  }
}
