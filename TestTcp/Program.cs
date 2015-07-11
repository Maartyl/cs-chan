using System.Threading.Tasks;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace TestTcp
{
  class MainClass {
    public static void Main(string[] args) {
      Action<object> cw = Console.WriteLine;
      cw("* any");
      cw(IPAddress.Any);
      cw(IPAddress.IPv6Any);
      cw("* loopback");
      cw(IPAddress.Loopback);
      cw(IPAddress.IPv6Loopback);
      cw("* none");
      cw(IPAddress.None);
      cw(IPAddress.IPv6None);
      cw("* bc4"); //trivia: there is no bc6, only multicast
      cw(IPAddress.Broadcast);

      TcpListener l = new TcpListener(IPAddress.Any, 7896);
      l.Start();
      l.BeginAcceptTcpClient(ar => {
        var c = l.EndAcceptTcpClient(ar);
        l.Stop();
        //var s = new StreamWriter(c.GetStream());
        //s.WriteLine("test writer : needed");
        //s.Flush();
        var bs = System.Text.Encoding.UTF8.GetBytes("test direct : not needed");
        c.GetStream().Write(bs, 0, bs.Length);
        c.Close();
      }, l);

      //start client
      var q = new TcpClient("localhost", 7896);
      var qs = new StreamReader(q.GetStream());
      Console.WriteLine(qs.ReadLine());
      // l.Stop();
    }
  }
}
