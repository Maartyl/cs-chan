using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Chan
{
  public abstract class NetChanTBase<T> : NetChanBase {
    //I present myself to world through membrane; using other side of it from the inside
    protected readonly IChan<T> World;
    protected readonly ISerDes<T> SerDes;

    protected NetChanTBase(NetChanConfig<T> cfg):base(cfg) {
      World = cfg.Channel;
      var sd = cfg.SerDes;
      if (sd == null)
        throw new ArgumentNullException("type(" + typeof(T) + ") is not serializable and requires valid SerDes`1");
      SerDes = sd;
    }

    protected Task SendMsg(T msg) {
      //this method could be in sender, but... who knows: might move, might be useful
      ushort length;
      var buff = sendBuffer; //in case someone changes the buffer
      bool couldReuseBuffer; //thanks to this: sends from 0: SendBytes will shift the data while merging
      try {
        //try reuse buffer : should work most of the time
        //this alows me to start writing after space for header: saves me from shifting data
        var ms = new MemoryStream(buff, Header.Size, buff.Length - Header.Size);
        SerDes.Serialize(ms, msg);
        length = (ushort) ms.Length;
        couldReuseBuffer = true;
      } catch (NotSupportedException ex) {
        //buffer too short: do again, able to resize and change buffer to created new: bigger
        //sadly: needs to shift: written from beginning

        //in case buffer was already maximal size and still wasn't enough
        if (buff.Length >= Header.Size + ushort.MaxValue)
          throw new NotSupportedException("messages over 64KB are not supported");

        //I know the current size was not enough: I know I can start there++ (it will be more)
        var ms = new MemoryStream(buff.Length + Header.Size);
        SerDes.Serialize(ms, msg);
        if (ms.Length > ushort.MaxValue)
          throw new NotSupportedException("messages over 64KB are not supported");
        length = (ushort) ms.Length;
        buff = ms.GetBuffer();
        couldReuseBuffer = false;
      }
      if (sendBuffer.Length < buff.Length)
        sendBuffer = buff;
      return SendBytes(CreateBaseMsgHeader(), buff, couldReuseBuffer ? Header.Size : 0, length);
    }
  }
}

