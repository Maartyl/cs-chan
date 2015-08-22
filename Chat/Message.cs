using System;

namespace Chat
{
  [Serializable]
  public class Message {
    public MessageType Type { get; private set; }

    public CmdArg Data { get; private set; }

    public Message(MessageType type, CmdArg data) {
      Type = type;
      Data = data;
    }

    public Message(CmdArg msg) : this(MessageType.Message, msg) {
      
    }

    public override string ToString() {
      switch (Type) {
        case MessageType.Message:
          return Data.ToString();
        default:
          return string.Format("[Msg({0}): {1}]", Type, Data);
      }
    }

    [Serializable]
    public enum MessageType {
      Connected,
      Disconnected,
      Message,
      SysMessage
    }
  }
}

