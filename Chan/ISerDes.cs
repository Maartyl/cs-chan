using System;
using System.IO;

namespace Chan
{
  public interface ISerDes<T> {
    void Serialize(Stream s, T obj);

    T Deserialize(Stream s);
  }
}

