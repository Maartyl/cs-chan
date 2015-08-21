using System;
using System.Windows.Forms;
using System.Drawing;

namespace Chat
{
  public class Gui {
    static readonly Font FONT = new Font("Monospace", 12);
    FormWithCompletion self;
    TextBox cmdBar;
    Panel area;
    RichTextBox msgBoard;
    Views views = new Views();

    public event Action<string> Command = cmd => {};
    public event Action<string> CompletionRequest = cmd => {};
    public event Action BoardChanged = () => {};

    public Gui(Action<Gui> onLoad) {
      self = new FormWithCompletion();

      area = new Panel();
      area.Dock = DockStyle.Fill;
      area.BorderStyle = BorderStyle.None;
      area.Margin = new Padding(0);
      area.Padding = new Padding(0);

      cmdBar = new TextBox();
      cmdBar.Font = FONT;
      cmdBar.Dock = DockStyle.Bottom;
      cmdBar.BorderStyle = BorderStyle.None;
      cmdBar.AcceptsTab = true;
      cmdBar.KeyPress += onCmdBarKeyPress;

      msgBoard = new RichTextBox();
      msgBoard.Font = FONT;
      msgBoard.Multiline = true;
      msgBoard.BorderStyle = BorderStyle.None;
      msgBoard.BackColor = SystemColors.Window;
      msgBoard.ReadOnly = true;
      msgBoard.ContextMenu = null;

      self.Controls.Add(cmdBar);
      self.Controls.Add(area);

      self.CompletionKeyPressed += () => CompletionRequest(cmdBar.Text);

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

    //show 'information' from system (not error, not message)
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

    public void ClearCmdLineIfSame(string orig) {
      //if it took long, user could loose changes
      PushCompletion("", orig);
    }

    public void PushCompletion(string cmdText, string orig) {
      //changes cmdBar if not changed by user meanwhile
      if (cmdBar.Text == orig) {
        cmdBar.Text = cmdText;
        cmdBar.Select(cmdText.Length, 0); //move cursor to end of line
      }
    }

    public void Exit() {
      self.Close();
    }

    public Action<T> InSTAThread<T>(Action<T> fn) {
      return t => {
        if (self.InvokeRequired)
          self.BeginInvoke(fn, t);
        else
          fn(t);
      };
    }

    void onCmdBarKeyPress(object sender, KeyPressEventArgs e) {
      if (e.KeyChar == (char) Keys.Return)
        Command(cmdBar.Text);
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

    class FormWithCompletion : Form {
      public event Action CompletionKeyPressed = ()=>{};

      //to capture Tab I need to override this: thus I had to subclass Form
      //it works everywhere in the form
      protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData) {
        if (keyData == Keys.Tab) {
          CompletionKeyPressed();
          return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
      }
    }
  }
}

