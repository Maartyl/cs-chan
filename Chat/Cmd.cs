using System;

namespace Chat
{
  public static class Cmd {

    public static readonly string Help = "help";
    public static readonly string Chat = "chat";
    public static readonly string Send = "send";
    public static readonly string Text = "text";
    public static readonly string Name = "name";
    public static readonly string NotifyError = "notifyError";
    public static readonly string NotifySystem = "notifySystem";
    public static readonly string Exit = "exit";
    public static readonly string ScrollDown = "down";
    public static readonly string ReceivedMsg = "receivedMsg";
    public static readonly string Host = "host";
    public static readonly string Join = "join";
    public static readonly string Server = "server";
    public static readonly string ServerStart = "server.start";
    public static readonly string ServerStop = "server.stop";
    public static readonly string Wsdl = "wsdl";
    public static readonly string WsdlStop = "wsdl.stop";
    public static readonly string Connect = "connect";
    public static readonly string Disconnect = "disconnect";
    public static readonly string NoCommand = " #nocommand";

    public static readonly string CompletionRequest = " #completion.request";
    public static readonly string CompletionResponse = " #completion.response";

    public static readonly string CmdAccepted = " #cmd.accepted";
    public static readonly string CmdParseRun = "parse-run";

    public static readonly string AfterGuiLoaded = " #after-gui-loaded";


    public static T ParseCommandInto<T>(string line, Func<string, string, T> cmdAndArgsConsumer) {
      var c = cmdAndArgsConsumer;

      /*
:<cmd> <arg>  -> cmd, arg
missing cmd: null
missing arg: null
:<cmd><end>   -> cmd, null
:<end> -> null, null
everything else -> text, line

i.e.
': a' -> null, a
':a ' -> a, null
':a  '-> a, ' '
       */
      if (!string.IsNullOrWhiteSpace(line)
          && line[0] == Settings.UserCommandStart) {
        line = line.Substring(1);
        if (string.IsNullOrEmpty(line))
          return c(null, null);
        if (char.IsWhiteSpace(line[0]))
          return c(null, line.Length == 1 ? null : line.Substring(1));

        var ln = line.Split((char[]) null/*whitespace*/, 2, StringSplitOptions.None);
        return c(ln[0], ln.Length == 1 || string.IsNullOrEmpty(ln[1]) ? null : ln[1]);
      } else {
        return c(Cmd.Text, line);
      }

    }

    /// trim left; find first whitespace; no ? c(line, null) : omitting the whitespace returns c(first, rest)
    public static T FirstAndRest<T>(string line, Func<string, string, T> c) {
      line = line.TrimStart();
      var firstWhitespace = line.IndexOf(char.IsWhiteSpace);
      if (firstWhitespace == -1)
        return c(line, (null));
      return c(line.Substring(0, firstWhitespace), (line.Substring(firstWhitespace + 1)));
    }
  }
}

