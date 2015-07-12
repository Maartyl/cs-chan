// using System.Threading.Tasks;
using System;

namespace Chan
{
  internal static class ExtensionHelpers {
    internal static string Format(this string s, object a) {
      string.Format(s, a);
    }

    internal static string Format(this string s, object a, object b) {
      string.Format(s, a, b);
    }

    internal static string Format(this string s, params object[] args) {
      string.Format(s, args);
    }
  }
}

