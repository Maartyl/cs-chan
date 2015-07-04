using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;

namespace Chan
{
  public class DebugCounter {
    Dictionary<string, Dictionary<string, int>> data = new Dictionary<string, Dictionary<string, int>>();

    [System.Diagnostics.Conditional("DEBUG")]
    public void Inc(string obj, string prop) {
      Dictionary<string, int> objData;
      if (data.TryGetValue(obj, out objData)) 
        lock (objData) {
          int val;
          if (!objData.TryGetValue(prop, out val))
            val = 0;
          objData[prop] = val + 1;
        }
      else {
        lock (data) 
          if (!data.TryGetValue(obj, out objData)) 
            data[obj] = new Dictionary<string, int>();
        Incg(obj, prop);
      }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public void Inc<T>(T obj, string prop) {
      Inc(typeof(T).Name, prop);
    }

    public T Inc<T>(string obj, string prop, T value) {
      Inc(obj, prop);
      return value;
    }

    public T Inc<T, TO>(TO obj, string prop, T value) {
      Inc(obj, prop);
      return value;
    }

    public void Print(TextWriter w) {
      lock (data) {
        foreach (var kv in data) {
          w.WriteLine(kv.Key + ":");
          foreach (var pv in kv.Value) 
            w.WriteLine("\t" + pv.Key + ": " + pv.Value);
        }
      }
    }

    public static DebugCounter Glob = new DebugCounter();

    [System.Diagnostics.Conditional("DEBUG")]
    public static void Incg(string obj, string prop) {
      Glob.Inc(obj, prop);
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static void Incg<T>(T obj, string prop) {
      Glob.Inc(obj, prop);
    }

    public static T Incg<T>(string obj, string prop, T value) {
      return Glob.Inc(obj, prop, value);
    }

    public static T Incg<T, TO>(TO obj, string prop, T value) {
      return Glob.Inc(obj, prop, value);
    }
  }
}

