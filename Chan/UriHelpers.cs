using System;

namespace Chan
{
  public static class UriHelpers {
    public static Uri Normalize(this Uri uri) {
      if (uri.IsLoopback)//==localhost: choose 1 for equality
        uri = new UriBuilder(uri) { Host = "127.0.0.1" }.Uri;
      return uri;
    }
  }
}

