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
      //Rpl().Wait();

      var s = new ChanStore();
      //var a = new ChanAsync<int>(); 
      var a = new QQQ(); 
      s.RegisterReceiver(new Uri("chan:test"), (IChanReceiver<long>) a);
      var r2 = s.GetReceiver<string>(new Uri("chan:test"));
     
      //DebugCounter.Glob.Print(Console.Error);
    }

    class QQQ : IChanReceiver<long>, IChanReceiver<int> {
      #region IChanReceiver implementation
      Task<int> IChanReceiver<int>.ReceiveAsync() {
        throw new NotImplementedException();
      }
      #endregion
      #region IChanReceiver implementation
      Task<long> IChanReceiver<long>.ReceiveAsync() {
        throw new NotImplementedException();
      }
      #endregion
      #region IChanBase implementation
      Task IChanBase.Close() {
        throw new NotImplementedException();
      }

      Task IChanBase.AfterClosed() {
        throw new NotImplementedException();
      }
      #endregion
    }

    static async Task Rpl() {
      var rT = new TaskCompletionSource< IChanReceiver<string>>();
      var sT = new TaskCompletionSource< IChanSender<string>>();
      var nT = InitNet(rT, sT);
      new ChanEvent<String>(await rT.Task, Console.WriteLine);
      await Task.WhenAny(nT, exec(await sT.Task));
    }

    static async Task exec(IChanSender<String> cs) {
      await cs.SendAsync("Hello");
      await cs.SendAsync("from");
      await cs.SendAsync("async channel");
      string s;
      while ((s = Console.ReadLine()) != null)
        await cs.SendAsync(s);
      await cs.Close();
      try {
        await cs.SendAsync("this should not be shown");
      } catch (TaskCanceledException) {
        Console.WriteLine("correctly thrown cancelled");
      }
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
      var sT = s.Start(42);
      var rT = r.Start(42);
      await Task.WhenAll(sT, rT);
    }
  }
}
