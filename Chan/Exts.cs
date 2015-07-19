// using System.Threading.Tasks;
using System;

namespace Chan
{
  //extension helpers
  internal static class Exts {
    internal static string Format(this string s, object a) {
      return string.Format(s, a);
    }

    internal static string Format(this string s, object a, object b) {
      return string.Format(s, a, b);
    }

    internal static string Format(this string s, params object[] args) {
      return string.Format(s, args);
    }

    internal static IChanReceiver<T> GetReceiver<T>(this IChanFactory<Nothing> f) {
      return f.GetReceiver<T>(null); //null == only value of Nothing
    }

    internal static IChanSender<T> GetSender<T>(this IChanFactory<Nothing> f) {
      return f.GetSender<T>(null); //null == only value of nothing
    }
  }
}

