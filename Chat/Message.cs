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

    [Serializable]
    public enum MessageType {
      Connected,
      Disconnected,
      Message,
      SysMessage
    }
  }
}

