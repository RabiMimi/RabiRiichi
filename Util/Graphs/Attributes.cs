using System;

namespace RabiRiichi.Util.Graphs {
    [AttributeUsage(AttributeTargets.Parameter)]
    public class InputAttribute : Attribute {
        public string Id = "";

        public InputAttribute(string id = "") {
            Id = id;
        }
    }

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

        public ProducesAttribute(string id) {
            Id = id;
        }
    }
}