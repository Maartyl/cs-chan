using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chat
{
  public static class Exts {

    public static Task PipeEx(this Task t, Connector conn) {
      return conn.PipeEx(t);
    }

    public static Task PipeEx(this Task t, Connector conn, string source) {
      return conn.PipeEx(source, t);
    }

    public static Task PipeEx(this Task t, Connector conn, string source, Action<Exception> alsoDo) {
      return conn.PipeEx(source, t, alsoDo);
    }

    public static void PipeEx(this Exception ex, Connector conn) {
      conn.PipeEx(ex);
    }

    public static void PipeEx(this Exception ex, Connector conn, string source) {
      conn.PipeEx(ex, source);
    }

    public static int IndexOf<T>(this IEnumerable<T> source, 
                                 Func<T, bool> predicate) {
      int i = 0;
      foreach (var element in source) {
        if (predicate(element))
          return i;
        i++;
      }
      return -1;
    }

    public static void Ignore<T>(this T self) {
      //pass
    }
  }
}

