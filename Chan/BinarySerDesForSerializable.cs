// using System.Threading.Tasks;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Chan
{
  public static class BinarySerDesForSerializable<T> {
    readonly static ISerDes<T> cached = null;

    static BinarySerDesForSerializable() {
      var t = typeof(T);
      var attrs = t.GetCustomAttributes(typeof(SerializableAttribute), false);
      if (attrs.Length == 0)
        return;
      cached = new SerializableWrapper();
    }

    /// SerDes for serializable types(T). If T is not serializable, returns null.
    public static ISerDes<T> SerDes{ get { return cached; } }

    private class SerializableWrapper : ISerDes<T> {
      #region ISerDes implementation
      public void Serialize(Stream s, T obj) {
        new BinaryFormatter().Serialize(s, obj);
      }

      public T Deserialize(Stream s) {
        return (T) new BinaryFormatter().Deserialize(s);
      }
      #endregion
    }
  }
}

