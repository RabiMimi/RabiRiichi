using RabiRiichi.Event.Listener;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HUtil = HoshinoSharp.Runtime.Util;

namespace RabiRiichi.Event {
    public class EventBus {
        public readonly Dictionary<(Type, Phase), List<ListenerBase>> Listeners =
            new Dictionary<(Type, Phase), List<ListenerBase>>();
        private readonly List<(uint, ListenerBase)> cachedListeners =
            new List<(uint, ListenerBase)>();

        public void Register<T>(Phase phase, ListenerBase listener)
            where T : EventBase {
            var key = (typeof(T), phase);
            if (!Listeners.TryGetValue(key, out var list)) {
                list = new List<ListenerBase>();
                Listeners.Add(key, list);
            }
            list.Add(listener);
        }

        public async Task Process(EventBase ev) {
            if (ev.phase != Phase.Inactive) {
                HUtil.Warn($"Invalid event phase: {ev}");
                return;
            }

            while (ev.NextPhase()) {
                if (ev.IsFinished) continue;
                cachedListeners.Clear();
                foreach (var (key, value) in Listeners) {
                    if (key.Item2 != ev.phase)
                        continue;
                    if (!key.Item1.IsAssignableFrom(ev.GetType()))
                        continue;
                    value.RemoveAll((listener) => listener.IsDisposed);
                    foreach (var listener in value) {
                        uint priority = listener.CanListen(ev);
                        if (priority != Priority.Never) {
                            cachedListeners.Add((priority, listener));
                        }
                    }
                }
                cachedListeners.Sort((lhs, rhs) => rhs.Item1.CompareTo(lhs.Item1));
                foreach (var (_, listener) in cachedListeners) {
                    if (listener.IsDisposed)
                        continue;
                    if (await listener.Handle(ev))
                        break;
                    if (ev.IsFinished)
                        break;
                }
            }
        }
    }
}
