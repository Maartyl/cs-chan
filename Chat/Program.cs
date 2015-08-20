using System.Threading.Tasks;
using System;

using Chan;

namespace Chat
{
  //TODO: first 2 msgs lost at client if Broadcast

  class MainClass {
    public static void Main(string[] args) {
      //const string msg = "Specify [port] for sender or [addr:port] for client";
      if (args.Length == 0) {
        //Console.WriteLine(msg); 
        GuiChat.Start();
      } else {
        var arg = args[0];
        int port;
        if (int.TryParse(arg, out port)) {
          MainServer(port);
        } else {
          MainClient(arg);
        }
      }
    }

    public static void MainServer(int port) {
      var s = Server.Start(port);
      var t = ReadLoop(s);
      Task.WaitAny(s.Task, t);
      s.Close().Wait();
    }

    public static void MainClient(string server_endpoint) {
      var s = Client.Start(server_endpoint);
      var t = ReadLoop(s);
      Task.WaitAny(s.Task, t);
      s.Close().Wait();
    }

    static async Task ReadLoop(SimpleSC s) {
      await Task.Yield();
      while (true) {
        var str = Console.ReadLine();
        if (str == null) {
          await s.Close();
          return;
        }
        await s.Send(str);
      }
    }
  }

  interface SimpleSC {
    Task Task{ get; }

    Task Send(string msg);

    Task Close();
  }

  class Server : SimpleSC {
    ChanStore store = new ChanStore();

    protected IChanSender<string> Sender { get; set; }

    public Task Send(string msg) {
      return Sender.SendAsync(msg);
    }

    public Task Close() {
      store.StopServer();
      return Sender.Close();
    }

    public Task Task { get; private set; }

    public static Server Start(int port) {
      //local should be 0.0.0.0
      // - like this, only listens from this computer
      //the other should be localhost or ip (for client)
      //var c2s = new Uri("chan://127.0.0.1:7897/client2server");
      var c2sLoc = new Uri("chan:client2server");
      //var s2c = new Uri("chan://127.0.0.1:7897/server2client");
      var s2cLoc = new Uri("chan:server2client");
      var s = new Server();
      var ss = s.store;
      var cfg = NetChanConfig.MakeDefault<string>();
      var t1 = ss.CreateNetChan(c2sLoc.AbsolutePath, cfg); //certainly necessary
      var t2 = ss.CreateNetChan(s2cLoc.AbsolutePath, cfg, ChanDistributionType.Broadcast);
      var t3 = ss.PrepareClientReceiverForType(cfg); //... I need the task... (and doesn't work always)
      var t4 = ss.PrepareClientSenderForType(cfg);   // keep as necessary

      ss.StartServer(port);

      ChanEvent.Listen(ss.GetReceiver<string>(c2sLoc), Console.WriteLine);
      s.Sender = ss.GetSender<string>(s2cLoc);

      //debug fast - this is 'client'
      //var sndrT = SendPeriodically(1200, "test", ss.GetSender<string>(c2s));
      s.Task = Task.WhenAny(failed(t1), failed(t2), failed(t3), failed(t4)/*, failed(sndrT)*/);
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

  class Client : SimpleSC {
    ChanStore store = new ChanStore();


    protected IChanSender<string> Sender {
      get;
      set;
    }

    public Task Send(string msg) {
      return Sender.SendAsync(msg);
    }

    public Task Close() {
      return Sender.Close();
    }

    public Task Task {
      get;
      private set;
    }

    public static Client Start(string host) {
      //local should be 0.0.0.0
      // - like this, only listens from this computer
      //the other should be localhost or ip (for client)
      var c2s = new Uri("chan://" + host + "/client2server");
      var s2c = new Uri("chan://" + host + "/server2client");
      var s = new Client();
      var ss = s.store;
      var cfg = NetChanConfig.MakeDefault<string>();
      var t3 = ss.PrepareClientReceiverForType(cfg); //... I need the task... (and doesn't work always)
      var t4 = ss.PrepareClientSenderForType(cfg);   // keep as necessary

      ChanEvent.Listen(ss.GetReceiver<string>(s2c), Console.WriteLine);
      s.Sender = ss.GetSender<string>(c2s);

      s.Task = Task.WhenAny((t3), (t4));
      return s;
    }
  }
}
