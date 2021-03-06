using System;

namespace RabiRiichi.Utils.Graphs {
    [AttributeUsage(AttributeTargets.Parameter)]
    public class ConsumesAttribute : Attribute {
        public string Id = "";

        public ConsumesAttribute(string id = "") {
            Id = id;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ProducesAttribute : Attribute {
        public string Id = "";
        public int Cost = 1024;

        public ProducesAttribute(string id = "") {
            Id = id;
        }
    }
}