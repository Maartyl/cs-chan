using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Chan;

namespace Chat
{
  class MainClass {
    public static void Main(string[] args) {
      
      var s = Server.Start();
      var s1T = s.Send("hello!");

      Console.WriteLine(@"
####################################      
########## Hello World! ############
####################################".Trim());

      Task.WhenAll(s1T, s.Task).Wait();
    }
  }

  class Server {
    ChanStore store = new ChanStore();

    protected Server() {
      
    }

    protected IChanSender<string> Sender {
      get;
      set;
    }

    public Task Send(string msg) {
      return Sender.SendAsync(msg);
    }


    public Task Task {
      get;
      private set;
    }

    public static Server Start() {
      //local should be 0.0.0.0
      // - like this, only listens from this computer
      //the other should be localhost or ip (for client)
      var c2s = new Uri("chan://127.0.0.1:7897/client2server");
      var s2c = new Uri("chan://127.0.0.1:7897/server2client");
      var s = new Server();
      var ss = s.store;
      var cfg = new NetChanConfig<string>();
      var t1 = ss.CreateNetChan(c2s.AbsolutePath, cfg);
      var t2 = ss.CreateNetChan(s2c.AbsolutePath, cfg);
      var b = new BasicHttpBinding();
      var t3 = ss.PrepareClientReceiverForType(cfg);
      var t4 = ss.PrepareClientSenderForType(cfg);
      ss.RegisterClientReceiverBinding(c2s, b);
      ss.RegisterClientSenderBinding(c2s, b);
      ss.RegisterClientSenderBinding(s2c, b);

      ss.StartServer(7897);

      var e = new ChanEvent<string>(ss.GetReceiver<string>(c2s), Console.WriteLine);
      e.ReceivedMessage += m => DbgCns.Trace("serv", "received", m);

      s.Sender = ss.GetSender<string>(s2c);

      //debug fast
      var sndrT = SendPeriodically(600, "test", ss.GetSender<string>(c2s));
      s.Task = Task.WhenAny(failed(t1), failed(t2), failed(t3), failed(t4), failed(sndrT));
      return s;
    }

    static async Task SendPeriodically(int ms, string msg, IChanSender<string> sender) {
      int i = 0;
      while (true) {
        await Task.Delay(ms);
        await sender.SendAsync(msg + i++);
      }
    }

    static async Task failed(Task t) {
      await await t.ContinueWith(x => x, TaskContinuationOptions.NotOnRanToCompletion);
    }
  }

  class Client {
    
  }
}
