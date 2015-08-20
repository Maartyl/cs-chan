using System;
using System.Windows.Forms;
using System.Drawing;
using System.Threading.Tasks;
using System.Threading;

namespace Chat
{
  public class Gui {
    static readonly Font FONT = new Font("Monospace", 12);
    Form self;
    TextBox cmdBar;
    Panel area;
    RichTextBox msgBoard;
    Views views = new Views();
    //TaskScheduler context = TaskScheduler.FromCurrentSynchronizationContext();
    /// (cmdName, cmdArgs) -> ok?
    public event Func<string, string, bool> Command = (cmd,arg) => (false);
    public event Action BoardChanged = () => {};

    public Gui(Action<Gui> onLoad) {
      self = new Form();

      area = new Panel();
      area.Dock = DockStyle.Fill;
      area.BorderStyle = BorderStyle.None;
      area.Margin = new Padding(0);
      area.Padding = new Padding(0);

      cmdBar = new TextBox();
      cmdBar.Font = FONT;
      cmdBar.Dock = DockStyle.Bottom;
      cmdBar.BorderStyle = BorderStyle.None;
      cmdBar.KeyPress += onCmdBarKeyPressIfEnter;

      msgBoard = new RichTextBox();
      msgBoard.Font = FONT;
      msgBoard.Multiline = true;
      msgBoard.BorderStyle = BorderStyle.None;
      msgBoard.BackColor = SystemColors.Window;
      msgBoard.ReadOnly = true;
      msgBoard.ContextMenu = null;

      self.Controls.Add(cmdBar);
      self.Controls.Add(area);

      self.Load += (src, ea) => onLoad(this);

      self.GotFocus += (src, ea) => cmdBar.Focus();
      area.GotFocus += (src, ea) => cmdBar.Focus();
      msgBoard.KeyPress += (src, ea) => {
        cmdBar.Focus();
        //TODO: doesn't work for shortcuts
        cmdBar.Paste(ea.KeyChar.ToString()); //insert at cursor
      };
    }

    public void SwitchTo(Control c) {
      if (area.Contains(c))
        return;

      c.Dock = DockStyle.Fill;
      area.Controls.Clear();
      area.Controls.Add(c);
    }

    public void SwitchToHelp(string help) {
      if (!object.ReferenceEquals(views.Help.Text, help))
        views.Help.Text = help;
      SwitchTo(views.Help);
    }

    public void SwitchToChat() {
      SwitchTo(msgBoard);
    }

    private void MsgBoardAppendColored(string text, Color clr) {
      var origClr = msgBoard.SelectionColor;
      msgBoard.Select(msgBoard.TextLength, 0);
      msgBoard.SelectionColor = clr;
      msgBoard.AppendText(text);
      msgBoard.SelectionColor = origClr;
    }

    public void ShowMessage(string sender, string message) {
      if (sender == null)
        sender = "?";
      MsgBoardAppendColored(sender, Color.Blue);
      MsgBoardAppendColored(": ", Color.Gray);
      MsgBoardAppendColored(message + "\n", Color.Black);
      BoardChanged();
    }

    public void ShowError(string sender, string message) {
      if (sender == null) {
        MsgBoardAppendColored(message + "\n", Color.Red);
      } else {
        MsgBoardAppendColored(sender, Color.Red);
        MsgBoardAppendColored(": ", Color.Gray);
        MsgBoardAppendColored(message + "\n", Color.Red);
      }
      BoardChanged();
    }

    public void ShowNotifySystem(string sender, string message) {
      if (sender == null) {
        MsgBoardAppendColored(message + "\n", Color.Green);
      } else {
        MsgBoardAppendColored(sender, Color.Green);
        MsgBoardAppendColored(": ", Color.Gray);
        MsgBoardAppendColored(message + "\n", Color.Green);
      }
      BoardChanged();
    }

    public void ScrollToBottom() {
      msgBoard.SelectionStart = msgBoard.Text.Length;
      msgBoard.ScrollToCaret();
    }

    public void Exit() {
      self.Close();
    }

    /// Well, this doesn't work: it's captured in async context, not the same as last Task...
    //private Task GuiContext { get { return Task.WhenAll().ContinueWith(t => {}, context); } }

    public Action<T> InSTAThread<T>(Action<T> fn) {
      return t => self.BeginInvoke(fn, t);
    }

    private void onCmdBarKeyPressIfEnter(object sender, KeyPressEventArgs e) {
      var orig = cmdBar.Text;
      if (e.KeyChar == (char) Keys.Return) 
        if (Cmd.ParseCommandInto(cmdBar.Text, Command))
          if (cmdBar.Text == orig)//if it took long, user could loose changes //should be synchronized, but...
            cmdBar.Text = ""; //TODO: consider: this entire feedback is possibly ridiculous...
    }

    [STAThread]
    public static void Start(Action<Gui> onLoad) {
      Application.EnableVisualStyles();
      Application.Run(new Gui(onLoad).self);
    }

    private class Views {
      public readonly TextBox Help;

      public Views() {
        var tb = new TextBox();
        tb.BorderStyle = BorderStyle.None;
        tb.Multiline = true;
        tb.Font = FONT;
        tb.BackColor = SystemColors.Window;
        tb.ReadOnly = true;
        tb.Text = @"No help provided yet: incorrect state";
        Help = tb;
      }
    }
  }
}

