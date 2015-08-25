using System.Threading.Tasks;
using System;
using Chan;

namespace Chat
{

  class MainClass {
    public static void Main(string[] args) {
      if (args.Length == 2 && args[0] == "--simple") {
        //const string msg = "Specify [port] for sender or [addr:port] for client";
        //Console.WriteLine(msg); 
        MainSimple(args[1]);
        return;
      }
      //---
      //MAYBE: support ops that change settings
      GuiChat.Start(new Settings(), (conn, store) => {
        //this is run after loaded gui
        //I will use this fn to execute program arguments
        //i.e. : any arguments will be interpreted as commands written in gui
        foreach (var arg in args)
          conn.RunOrDefault(Cmd.CmdParseRun, arg);
      });

      #if DEBUG
      Console.Error.WriteLine("main-thread exit");
      #endif
    }

    //this is for simple Chan testing
    public static void MainSimple(string arg) {
      int port;
      if (int.TryParse(arg, out port)) {
        SimpleServer(port);
      } else {
        SimpleClient(arg);
      }
    }

    public static void SimpleServer(int port) {
      var s = Server.Start(port);
      var t = SimpleReadLoop(s);
      Task.WaitAny(s.Task, t);
      s.Close().Wait();
    }

    public static void SimpleClient(string server_endpoint) {
      var s = Client.Start(server_endpoint);
      var t = SimpleReadLoop(s);
      Task.WaitAny(s.Task, t);
      s.Close().Wait();
    }

    static async Task SimpleReadLoop(SimpleSC s) {
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
    Task Task { get; }

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
      return store.CloseAll();
    }

    public Task Task { get; private set; }

    public static Server Start(int port) {
      var c2sLoc = new Uri("chan:client2server");
      var s2cLoc = new Uri("chan:server2client");
      var s = new Server();
      var ss = s.store;

      var cfg = NetChanConfig.MakeDefault<string>();
      var t1 = ss.CreateNetChan(c2sLoc.AbsolutePath, cfg);
      var t2 = ss.CreateNetChan(s2cLoc.AbsolutePath, cfg, ChanDistributionType.Broadcast);

      ss.StartServer(port);

      ChanEvent.Listen(ss.GetReceiver<string>(c2sLoc), Console.WriteLine);
      s.Sender = ss.GetSender<string>(s2cLoc);

      s.Task = Task.WhenAny(t1, t2);
      return s;
    }
  }

  class Client : SimpleSC {
    ChanStore store = new ChanStore();


    protected IChanSender<string> Sender { get; set; }

    public Task Send(string msg) {
      return Sender.SendAsync(msg);
    }

    public Task Close() {
      return store.CloseAll();
    }

    public Task Task { get; private set; }

    public static Client Start(string host) {
      var c2s = new Uri("chan://" + host + "/client2server");
      var s2c = new Uri("chan://" + host + "/server2client");
      var s = new Client();
      var ss = s.store;

      var cfg = NetChanConfig.MakeDefault<string>();
      var t3 = ss.PrepareClientReceiverForType(cfg); //... I need the task...
      var t4 = ss.PrepareClientSenderForType(cfg);

      ChanEvent.Listen(ss.GetReceiver<string>(s2c), Console.WriteLine);
      s.Sender = ss.GetSender<string>(c2s);

      s.Task = Task.WhenAny(t3, t4);
      return s;
    }
  }
}
