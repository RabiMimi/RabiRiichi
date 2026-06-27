using System;

namespace RabiRiichi.Utils.Graphs {
  [AttributeUsage(AttributeTargets.Parameter)]
  public class ConsumesAttribute(string id = "") : Attribute {
    public string Id = id;
  }

  [AttributeUsage(AttributeTargets.Method)]
  public class ProducesAttribute(string id = "") : Attribute {
    public string Id = id;
    public int Cost = 1024;
  }
}