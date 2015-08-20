using System;

namespace Chat
{
  public struct CmdArg {
    private CmdArg(string source, string text) : this() {
      Source = source;
      Text = text;
    }

    private CmdArg(string text) : this(null, text) {

    }

    public string Source { get; private set; }

    public string Text { get; private set; }

    public CmdArg Trim() {
      var src = Source == null ? null : Source.Trim();
      if (src == "")
        src = null;
      var txt = Text == null ? null : Text.Trim();
      if (txt == "")
        txt = null;
      return Of(src, txt);
    }

    public static implicit operator CmdArg(string text) {
      return Of(text);
    }

    public static implicit operator string(CmdArg arg) {
      return arg.Text;
    }

    public static CmdArg Of(string text) {
      return new CmdArg(text);
    }

    public static CmdArg Of(string source, string text) {
      return new CmdArg(source, text);
    }
  }

  public static class CmdArgExts {
    /// <summary>
    /// Specifies the source of string becoming argument.
    /// </summary>
    /// <returns>command argument</returns>
    /// <param name="text">Text.</param>
    /// <param name="source">Source.</param>
    public static CmdArg ArgSrc(this string text, string source) {
      return CmdArg.Of(source, text);
    }
  }
}

