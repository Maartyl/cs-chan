using System.Threading;
using System;

namespace Chan
{
  //debug to console
  public static class DbgCns {
    [System.Diagnostics.Conditional("TRACE")]
    public static void Trace(string source, string what, string data = null) {
      var tid = Thread.CurrentThread.ManagedThreadId;
      var time = Convert.ToBase64String(BitConverter.GetBytes(DateTime.Now.Ticks)).TrimEnd('=');
      if (data == null)
        Console.Error.WriteLine("{0}@{1,2:N0} {2} #{3}", time, tid, source, what);
      else 
        Console.Error.WriteLine("{0}@{1,2:N0} {2} #{3} ({4})", time, tid, source, what, data);
    }

    [System.Diagnostics.Conditional("TRACE")]
    public static void Trace<T>(T source, string what, string data = null) {
      Trace(typeof(T).Name, what, data);
    }

    public static DateTime DecodeTime(string time64) {
      time64 = time64.Substring(0, 11) + "="; //follows format by Trace
      return new DateTime(BitConverter.ToInt64(Convert.FromBase64String(time64), 0));
    }
    //readonly Func<string,DateTime> consoleDecode = x => new DateTime(BitConverter.ToInt64(Convert.FromBase64String(x.Substring(0, 11) + "="), 0));
  }
}

