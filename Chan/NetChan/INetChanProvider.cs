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
    public int Port { get; internal set; }

    public uint Key { get; internal set; }

    public ChanDistributionType Type { get; internal set; }
    //---
    public string ErrorMessage { get; internal set; }

    ///if error == exception: the name of the type of the exception
    public string ErrorType { get; internal set; }

    ///if error == exception: .HResult
    public int ErrorCode { get; internal set; }

    public bool IsOk{ get { return ErrorCode == 0 && ErrorMessage == null && ErrorType == null; } }
  }

  public class NetChanProviderException : Exception {
    public NetChanConnectionInfo Info { get; private set; }

    public Uri RequestUri { get; private set; }

    public NetChanProviderException(NetChanConnectionInfo info, Uri requestUri) : base(info.ErrorType + ": " + info.ErrorMessage) {
      RequestUri = requestUri;
      if (info.ErrorCode != 0)
        this.HResult = info.ErrorCode;
      Info = info;
    }
  }
}

