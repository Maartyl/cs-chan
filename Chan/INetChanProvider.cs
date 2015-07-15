// using System.Threading.Tasks;
using System;
using System.ServiceModel;

namespace Chan
{
  [ServiceContract] 
  public interface INetChanProvider {    
    [OperationContract] 
    NetChanConnectionInfo RequestSender(Uri chanName);

    [OperationContract] 
    NetChanConnectionInfo RequestReceiver(Uri chanName);
  }

  [Serializable]
  public class NetChanConnectionInfo {
    public int Port{ get; internal set; }

    public uint Key { get; internal set; }

    public ChanDistributionType Type { get; internal set; }
    //---
    public string ErrorMessage{ get; internal set; }

    public int ErrorCode{ get; internal set; }

    public bool IsOk{ get { return ErrorCode == 0 && ErrorMessage == null; } }
  }
}

