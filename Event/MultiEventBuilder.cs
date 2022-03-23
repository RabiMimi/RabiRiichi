using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
using System.Collections.Generic;
using System.Linq;


namespace RabiRiichi.Event {
    public interface IEventBuilder {
        EventBase Build();
    }
    public class MultiEventBuilder {
        public readonly List<EventBase> events = new();
        public readonly List<IEventBuilder> builders = new();

        public MultiEventBuilder AddEvent(EventBase e) {
            events.Add(e);
            return this;
        }

        public MultiEventBuilder AddBuilder(IEventBuilder e) {
            builders.Add(e);
            return this;
        }

        public MultiEventBuilder AddAgari(EventBase parent, int fromPlayer, GameTile incoming, AgariInfo info) {
            if (builders.Find(b => b is AgariEvent.Builder) is not AgariEvent.Builder builder) {
                builder = new AgariEvent.Builder(parent, fromPlayer, incoming);
                builders.Add(builder);
            }
            builder.Add(info);
            return this;
        }

        public List<EventBase> Build() {
            events.AddRange(builders.Select(builder => builder.Build()).Where(e => e != null));
            builders.Clear();
            return events;
        }

        public List<EventBase> BuildAndQueue(EventQueue queue) {
            var ret = Build();
            foreach (var e in ret) {
                queue.Queue(e);
            }
            return ret;
        }
    }
}