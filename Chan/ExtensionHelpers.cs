// using System.Threading.Tasks;
using System;

namespace Chan
{
  internal static class ExtensionHelpers {
    internal static string Format(this string s, object a) {
      return string.Format(s, a);
    }

    internal static string Format(this string s, object a, object b) {
      return string.Format(s, a, b);
    }

    internal static string Format(this string s, params object[] args) {
      return string.Format(s, args);
    }
  }
}

