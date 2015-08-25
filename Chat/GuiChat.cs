using System;
using System.Linq;
using System.Threading.Tasks;
using Chan;

namespace Chat
{
  static class GuiChat {

    public static void Start(Settings settings, Action<Connector, ChanStore> initExtra) {
      #if DEBUG
      //testParsing();
      #endif

      var conn = new Connector();
      Init(settings, conn, initExtra);
      StartGui(conn, settings);
    }

    //originally: only run when needed to start server (the first time)
    // - didn't work well; now every server has it's own ChanStore (WCF reasons, mainly)
    //Unit so it can be used in Lazy
    static Unit InitServer(ChanStore store, Connector conn) {
      //simple default config for now
      var dfltCfg = NetChanConfig.MakeDefault<Message>();

      var bcFailT = store.CreateNetChan(Settings.ChanBroadcastName, dfltCfg, ChanDistributionType.Broadcast);
      var bcUri = new Uri("chan:" + Settings.ChanBroadcastName);
      var bcr = store.GetReceiver<Message>(bcUri);
      var bcs = store.GetSender<Message>(bcUri);
      var bcPipeT = bcr.Pipe(bcs); //connect server side of pipe: return back to everyone
      bcFailT.PipeEx(conn, "server: bc chan");
      bcPipeT.PipeEx(conn, "server: bc pipe");
      bcr.AfterClosed().PipeEx(conn, "server: bc receiver"); //I don't think these can ever fail...
      bcs.AfterClosed().PipeEx(conn, "server: bc sender");

      return null;
    }

    static void Init(Settings settings, Connector conn, Action<Connector, ChanStore> initExtra) { 
      var store = new ChanStore();
      var client = new ChatClient(settings, store, conn);

      //simple default config for now
      var dfltCfg = NetChanConfig.MakeDefault<Message>();

      //client
      var rFailT = store.PrepareClientReceiverForType(dfltCfg);
      var sFailT = store.PrepareClientSenderForType(dfltCfg);
      rFailT.PipeEx(conn, "store: receiver cache");
      sFailT.PipeEx(conn, "store: sender cache");
      conn.Register(Cmd.Send, client.BroadcastMessage);
      conn.Register(Cmd.Connect, client.Connect);
      conn.Register(Cmd.Disconnect, _ => client.Disconnect());
      conn.Register(Cmd.Join, a => {
        var args = (a.Text ?? "").Split(new []{ ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length > 2)
          conn.RunError("requires 1 or 2 arguments <?host:port> <name>".ArgSrc("join"));
        else ((Func<Func<string, string, bool>, bool>) (cont => { //I could use ifs; this is more obvious to me...
            switch (args.Length) { //host, name
              case 1:
                return cont(null, args[0]);
              case 2:
                return cont(args[0], args[1]);
              default:
                return cont(null, null);
            } // apply args^ to connect and (name if != null)
          }))((host, name) => conn.RunOrDefault(Cmd.Connect, host) && (name != null) && conn.RunOrDefault(Cmd.Name, name));
      });

      //server
      //it reregisters it's commands if started (to ones created in start fn)
      Action<CmdArg> serverStartOff = null; //~ referenced from itself
      Action<CmdArg> serverStopOff = _ => conn.RunError("not running".ArgSrc("server"));
      serverStartOff = a => {
        var portS = a.Text;
        int port;
        if (!int.TryParse(portS, out port)) {
          //couldn't parse: try default
          if (settings.DefaultServerPort == -1) {
            conn.RunError("requires port (default not allowed)".ArgSrc(Cmd.ServerStart + " " + portS));
            return;
          }
          port = settings.DefaultServerPort;
        }
        //port OK: try creating and starting server:
        try {
          var serverStore = new ChanStore();
          InitServer(serverStore, conn);
          serverStore.StartServer(port);

          //created fine: reregister
          conn.Reregister(Cmd.ServerStart, _ => conn.RunError(("already running (port: " + port + ")").ArgSrc("server")));
          conn.Reregister(Cmd.ServerStop, _ => {
            try {
              serverStore.StopServer();//stop listening for new
              //kill stuff in ChanStore: this will force shut the open channels and close clients
              serverStore.CloseAll().PipeEx(conn, "server.stop (in clear)");

              conn.RunNotifySystem("stopped".ArgSrc("server"));
            } catch (Exception ex) {
              ex.PipeEx(conn, "server.stop");
            } finally {
              conn.Reregister(Cmd.ServerStop, serverStopOff); //use original handlers again
              conn.Reregister(Cmd.ServerStart, serverStartOff);
            }
          });

          conn.RunNotifySystem("started".ArgSrc("server"));
        } catch (Exception ex) {
          ex.PipeEx(conn, Cmd.ServerStart + " " + port);
        }
      };

      conn.Register(Cmd.ServerStart, serverStartOff);
      conn.Register(Cmd.ServerStop, serverStopOff);
      conn.Register(Cmd.Host, async port => {
        try {
          if (string.IsNullOrWhiteSpace(port)) {
            if (settings.DefaultServerPort == -1)
              throw new ArgumentException("requires port (default not allowed)");
            port = settings.DefaultServerPort.ToString();
          }

          if (await conn.RunOrDefaultAsync(Cmd.ServerStart, port)) {
            //~ok: problem: this is run after known, whether Cmd.Server exists, not after server started
            //: connect waits until started; ok
            // - no it doesn't: it cannot: it's elsewhere...
            await Task.Delay(50); //wait a little instead
            await conn.RunOrDefaultAsync(Cmd.Connect, "localhost:" + port);
          }
        } catch (Exception ex) {
          ex.PipeEx(conn, Cmd.Host + " " + port);
        }
      });

      //translate text to send
      conn.Register(Cmd.Text, a => {
        if (!string.IsNullOrWhiteSpace(a))
          conn.RunOrDefault(Cmd.Send, a.Text.Trim());
      });
      //get/change name
      conn.Register(Cmd.Name, a => {
        var arg = a.Text;
        if (string.IsNullOrWhiteSpace(arg)) {
          //get name
          conn.RunNotifySystem(client.ClientName.ArgSrc("clinet name"));
        } else {
          //set name
          client.ClientName = arg;
        }
      });

      //parse line into command and run it
      conn.Register(Cmd.CmdParseRun, a => {
        var ok = Cmd.ParseCommandInto(a, (cmd, arg) => conn.RunOrDefault(cmd ?? Cmd.NoCommand, arg));
        if (ok)
          conn.RunOrDefault(Cmd.CmdAccepted, a);
      });

      //command completion
      conn.Register(Cmd.CompletionRequest, a => {
        string arg = a;
        if (string.IsNullOrWhiteSpace(arg) || arg[0] != Settings.UserCommandStart || arg.Contains(' '))
          //for now: only completion of main command is suppoerted
          return;
        
        arg = arg.Substring(1); //remove commandStart char (:)
        var possibles = conn.Keys.Where(k => k.StartsWith(arg)).ToArray();
        if (possibles.Length == 0)
          //no option to help with
          return;

        //if more then 1: find longest common prefix (starting at already known: len of arg
        //if 1: append ' ' as it is both useful and shows the cmd is complete
        string completedCmd = possibles.Length == 1 ? possibles[0] + ' ' 
          : longestCommonPrefix(possibles, arg.Length);
        conn.RunOrDefault(Cmd.CompletionResponse, (Settings.UserCommandStart + completedCmd).ArgSrc(a));
      });

      conn.Reregister(Cmd.AfterGuiLoaded, _ => {
        initExtra(conn, store);

        //in AfterGuiLoaded because Gui registers Exit through Add, not Merge
        conn.Coregister(Cmd.Exit, __ => {
          //dirty way of making sure every WCF server is cosed, so the app doesn't hang after GUI thread finished
          conn.Reregister(Cmd.NotifyError, err => Console.Error.WriteLine(string.Format("{0}:! {1}", err.Source, err.Text)));
          conn.Reregister(Cmd.NotifySystem, err => Console.WriteLine(string.Format("{0}:: {1}", err.Source, err.Text)));
          conn.RunOrDefault(Cmd.Disconnect); //this is probaly not necesary, but why not...
          conn.RunOrDefault(Cmd.ServerStop);
          conn.RunOrDefault(Cmd.WsdlStop);
        });
      });

      conn.Register(Cmd.Wsdl, a => {
        int port;
        if (!int.TryParse(a, out port)/*try port from argument*/
            && (port = settings.DefaultWsdlPort) < 0/*try default; if (<0):*/
            && conn.RunError("default port not allowed".ArgSrc(Cmd.Wsdl)))
          return;

        try {
          var s = new ChanStore(port);
          s.StartServer(port);

          conn.RunNotifySystem(string.Format("running :{0}/ChanStore", port).ArgSrc(Cmd.Wsdl));
          conn.Register(Cmd.WsdlStop, _ => {
            s.StopServer();
            conn.Reregister(Cmd.WsdlStop, null);
            conn.RunNotifySystem("stopped".ArgSrc(Cmd.Wsdl));
          });
        } catch (Exception ex) {
          ex.PipeEx(conn, Cmd.Wsdl);
        }
      });

      #if DEBUG
      conn.Register("test", a => {
        conn.Run(Cmd.NotifyError, "test error".ArgSrc("err source"));
        conn.Run(Cmd.NotifyError, "test error without source");

        conn.Run(Cmd.NotifySystem, "test system".ArgSrc("system source"));
        conn.Run(Cmd.NotifySystem, "test system without source");

        conn.Run(Cmd.ReceivedMsg, "test msg".ArgSrc("msg source"));
        conn.Run(Cmd.ReceivedMsg, "test msg without source");
        conn.Run(Cmd.Help);
        conn.Run(Cmd.Chat);
      });
      #endif
    }

    static void StartGui(Connector conn, Settings settings) {
      Gui.Start(x => {
        //if changes UI: have to use this
        Action<string, Action<CmdArg>> xRegister = (cmd, a) => conn.Register(cmd, x.InSTAThread(a));
        var help = @"Help:
Type (messages) to line at bottom of window.
Commands start with ':'. 
All messages are trimmed by default, so to write ':-)' just write ' :-)'.
The '#' are not actualy recognized comment starts.

:chat               # show message board
:server.start <port># starts server and DOES NOT connect to it
:server.stop
:connect <host:port># default port is " + settings.DefaultServerPort + @"
:disconnect         # closes client
:host <port>        # :server.start, :connect localhost
:join <host> <name> # :connect <host>, :name <name>
:name <new name>    # get or set; 'anon' by default, (' '->'_')
:down, :d           # scroll to end of message board
:exit
:help, :h           # show this help
:send <msg>         # send msg as it is ; even just whitespace / nothing...
:text <theText>     # alias ~ :send <trim($theText) ?? cancel> 
                    # handles 'just text' 
                    # wouldn't send nothing
if line doesn't start with ':': gets translated to: ':text <$line>'
";

        xRegister(Cmd.Help, _ => x.SwitchToHelp(help));
        xRegister(Cmd.Chat, _ => x.SwitchToChat());
        xRegister(Cmd.Exit, _ => x.Exit());
        xRegister(Cmd.ScrollDown, _ => x.ScrollToBottom());
        xRegister(Cmd.NotifyError, a => x.ShowError(a.Source, a.Text));
        xRegister(Cmd.NotifySystem, a => x.ShowNotifySystem(a.Source, a.Text));
        xRegister(Cmd.ReceivedMsg, a => x.ShowMessage(a.Source, a.Text));
        xRegister(Cmd.CmdAccepted, a => x.ClearCmdLineIfSame(a));
        xRegister(Cmd.CompletionResponse, a => x.PushCompletion(a, a.Source));

        conn.Alias("d", Cmd.ScrollDown);
        conn.Alias("h", Cmd.Help);
        conn.Alias("quit", Cmd.Exit);

        conn.Run(Cmd.Help);

        x.Command += line => conn.RunOrDefault(Cmd.CmdParseRun, line);
        x.CompletionRequest += line => conn.RunOrDefault(Cmd.CompletionRequest, line);
        x.BoardChanged += () => conn.Run(Cmd.Chat);

        conn.RunOrDefault(Cmd.AfterGuiLoaded);
      });
    }

    private static void testParsing() {
      //yes, testing frameworks are better: I needed a little more feedback, though...
      Func<string,string,string, bool> testParse = (x, expectedCmd, expectedArgs) => Cmd.ParseCommandInto(x, (cmd, args) => {
        //        if (cmd == null)
        //          Console.WriteLine("cmd is null");
        //        if (args == null)
        //          Console.WriteLine("args is null");

        Func<string, string> show = s => {
          s = s ?? "null";
          if (string.IsNullOrWhiteSpace(s))
            s = string.Format("ws:{0}", s.Length);
          return s;
        };

        if (cmd != expectedCmd) {
          Console.WriteLine("in:'{2}'; cmd is: {0}; but expected: {1}", show(cmd), show(expectedCmd), show(x));  
          return (false);
        }
        if (args != expectedArgs) {
          Console.WriteLine("in:'{2}'; args is: {0}; but expected: {1}", show(args), show(expectedArgs), show(x));  
          return (false);
        }

        return (cmd != null);
      });

      Console.WriteLine("test start");  
      testParse(":", null, null);
      testParse(": ", null, null);
      testParse(":  ", null, " ");
      testParse(":   ", null, "  ");
      testParse(": ku hi", null, "ku hi");
      testParse(":   hi hi", null, "  hi hi");
      testParse(":a", "a", null);
      testParse(":a ", "a", null);
      testParse(":a  ", "a", " ");
      testParse(":a   ", "a", "  ");
      testParse(":longer", "longer", null);
      testParse(":longer ", "longer", null);
      testParse(":longer  ", "longer", " ");
      testParse(":has arg", "has", "arg");
      testParse(":has  arg", "has", " arg");
      testParse(":has  arg ", "has", " arg ");
      testParse(":has  arg1 arg2 arg3 ", "has", " arg1 arg2 arg3 ");
      //text
      testParse(" :has  arg ", "text", " :has  arg ");
      testParse("", "text", "");
      testParse("  ", "text", "  ");
      testParse(null, "text", null);
      testParse("asdf", "text", "asdf");
      Console.WriteLine("test end");  

    }

    static string longestCommonPrefix(string[] strings, int prefixLen = 0) {
      //http://codereview.stackexchange.com/a/46967
      // - Java solution; rewrote to C#
      //changed to include arg for start of prefixLen
      if (strings.Length == 0)
        return "";   // Or maybe return null?

      for (; prefixLen < strings[0].Length; prefixLen++) {
        char c = strings[0][prefixLen];
        for (int i = 1; i < strings.Length; i++) {
          if (prefixLen >= strings[i].Length ||
              strings[i][prefixLen] != c) {
            // Mismatch found
            return strings[i].Substring(0, prefixLen);
          }
        }
      }
      return strings[0];
    }
  }
}
