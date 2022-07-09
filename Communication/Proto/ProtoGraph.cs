using RabiRiichi.Utils.Graphs;

namespace RabiRiichi.Communication.Proto {
    public class ProtoGraph : ProducerGraph {
        public ProtoGraph() {
            Register<ProtoConverters>();
        }
    }
}