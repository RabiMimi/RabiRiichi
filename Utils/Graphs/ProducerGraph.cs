using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using ProducerRequirement = System.ValueTuple<string, System.Type>;

namespace RabiRiichi.Utils.Graphs {
    public static class ProducerGraphExtensions {
        public static bool Satisfies(this ProducerRequirement produced, ProducerRequirement required) {
            return produced.Item1 == required.Item1 && produced.Item2.IsAssignableTo(required.Item2);
        }
    }

    public class ProducerGraphNode {
        public readonly object instance;
        public readonly MethodInfo method;
        public readonly ProducerRequirement produces;
        public readonly int cost;
        public readonly List<ProducerRequirement> requires = new();

        public int IndexOfRequirement(ProducerRequirement item) {
            return requires.FindIndex(x => item.Satisfies(x));
        }

        public ProducerGraphNode(object instance, MethodInfo method) {
            this.instance = instance;
            this.method = method;
            var returnType = method.ReturnType;
            var attr = method.GetCustomAttribute<ProducesAttribute>();
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)) {
                returnType = returnType.GetGenericArguments()[0];
            }
            produces = (attr.Id, returnType);
            cost = attr.Cost;
            foreach (var param in method.GetParameters()) {
                if (param.GetCustomAttribute<ConsumesAttribute>() is ConsumesAttribute cAttr) {
                    requires.Add((cAttr.Id, param.ParameterType));
                }
            }
        }
    }

    public class ProducerGraphExecutionContext : IEquatable<ProducerGraphExecutionContext> {
        private readonly Dictionary<string, List<object>> produces = new();
        private readonly List<ProducerRequirement> inputs = new();
        private readonly ProducerGraph graph;

        private void AddProduces(string id, object obj) {
            if (!produces.TryGetValue(id, out var list)) {
                list = new List<object>();
                produces[id] = list;
            }
            list.Add(obj);
        }

        public ProducerGraphExecutionContext(ProducerGraph graph) {
            produces[""] = new List<object> { graph };
            this.graph = graph;
        }

        public bool TryGetOutput(Type type, string id, out object obj) {
            if (!produces.TryGetValue(id, out var list)) {
                obj = null;
                return false;
            }
            obj = list.FirstOrDefault(x => x.GetType().IsAssignableTo(type));
            return obj != null;
        }

        public ProducerGraphExecutionContext SetInput(string id, object obj) {
            if (!produces.TryGetValue(id, out var list)) {
                list = new List<object>();
                produces[id] = list;
            }
            list.Add(obj);
            inputs.Add((id, obj.GetType()));
            return this;
        }

        public ProducerGraphExecutionContext SetInput(object obj)
            => SetInput("", obj);

        public T Execute<T>(string id = "") {
            try {
                return ExecuteAsync<T>(id).Result;
            } catch (AggregateException e) {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                // Let compiler know that the above line always throws.
                throw;
            }
        }

        public async Task<T> ExecuteAsync<T>(string id = "") {
            var path = graph.FindPath(this, typeof(T), id);
            if (path == null) {
                throw new ArgumentException($"No path to produce {typeof(T)} with id {id}");
            }
            foreach (var node in path) {
                var parameters = node.node.method.GetParameters().Select(p => {
                    Logger.Assert(TryGetOutput(p.ParameterType,
                        p.GetCustomAttribute<ConsumesAttribute>().Id, out var obj),
                        $"No input found for {p.Name}");
                    return obj;
                });
                var ret = node.node.method.Invoke(node.node.instance, parameters.ToArray());
                if (ret is Task task) {
                    await task;
                    ret = task.GetType().GetProperty("Result").GetValue(task);
                }
                AddProduces(node.node.produces.Item1, ret);
            }
            Logger.Assert(TryGetOutput(typeof(T), id, out var obj), $"No output of type {typeof(T)} with id {id}");
            return (T)obj;
        }

        public override int GetHashCode() {
            return inputs.Aggregate(0, (a, b) => a ^ b.GetHashCode());
        }

        public bool Equals(ProducerGraphExecutionContext other) {
            if (other == null) {
                return false;
            }
            return inputs.SequenceEqual(other.inputs);
        }

        public override bool Equals(object obj) {
            return Equals(obj as ProducerGraphExecutionContext);
        }
    }

    public class ProducerGraph {
        public class NodeContext {
            public const int INIT_COST = int.MaxValue >> 1;
            private int visitedTimeStamp;
            private readonly NodeContext[] predecessors;
            public readonly ProducerGraphNode node;
            public int dist;

            public bool IsVisited(int time) => visitedTimeStamp == time;
            public bool IsInput => node == null;
            public bool IsValid => dist < INIT_COST;

            public bool TryReset(int timeStamp) {
                if (!IsVisited(timeStamp)) {
                    dist = node.requires.Count == 0 ? 0 : INIT_COST;
                    Array.Fill(predecessors, null);
                    visitedTimeStamp = timeStamp;
                    return true;
                }
                return false;
            }

            private bool UpdateDist() {
                if (predecessors.Length == 0) {
                    return false;
                }
                int newDist = predecessors.Max(p => p?.dist ?? INIT_COST) + node.cost;
                if (newDist >= dist) {
                    return false;
                }
                dist = newDist;
                return dist != INIT_COST;
            }

            public bool UpdatePredecessor(int index, NodeContext pred) {
                if (predecessors[index] == null || pred.dist < predecessors[index].dist) {
                    predecessors[index] = pred;
                    return UpdateDist();
                }
                return false;
            }

            public IEnumerable<NodeContext> GetPath(int timeStamp) {
                if (IsInput || IsVisited(timeStamp)) {
                    yield break;
                }
                // Reuse this timestamp, but no need to reset node
                visitedTimeStamp = timeStamp;
                foreach (var path in predecessors.SelectMany(pred => pred.GetPath(timeStamp))) {
                    yield return path;
                }
                yield return this;
            }

            public NodeContext(ProducerGraphNode node) {
                this.node = node;
                if (node != null) {
                    predecessors = new NodeContext[node.requires.Count];
                    Logger.Assert(TryReset(-1), "Failed to reset producer graph node");
                }
            }
        }

        /// <summary>
        /// A dummy node that tells a requirement is fulfilled by input.<br/>
        /// Never use its methods.
        /// </summary>
        private static readonly NodeContext INPUT_NODE = new(null) { dist = 0 };

        private readonly Dictionary<ProducerRequirement, List<NodeContext>> producerLookup = new();
        private readonly List<NodeContext> nodeCtxs = new();
        private readonly Dictionary<(ProducerGraphExecutionContext, string, Type), NodeContext[]> pathCache = new();
        private AutoIncrementInt timeStamp = new();

        public ProducerGraph Register(object obj, MethodInfo[] methods) {
            foreach (var method in methods) {
                if (method.GetCustomAttribute<ProducesAttribute>() == null) {
                    continue;
                }
                var invalidParam = method.GetParameters().FirstOrDefault(
                    p => p.GetCustomAttribute<ConsumesAttribute>() == null);
                if (invalidParam != null) {
                    Logger.Warn($"Producer method {method.Name} has invalid parameter {invalidParam.Name}");
                    continue;
                }
                var nodeCtx = new NodeContext(new ProducerGraphNode(method.IsStatic ? null : obj, method));
                nodeCtxs.Add(nodeCtx);
                if (!producerLookup.TryGetValue(nodeCtx.node.produces, out var list)) {
                    list = new List<NodeContext>();
                    producerLookup[nodeCtx.node.produces] = list;
                }
                list.Add(nodeCtx);
            }
            return this;
        }

        public ProducerGraph Register(object obj)
            => Register(obj, obj.GetType().GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        public ProducerGraph Register<T>() where T : class
            => Register(null, typeof(T).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));

        public ProducerGraphExecutionContext Build() => new(this);

        private int FindPath(ProducerGraphExecutionContext context, NodeContext node) {
            if (!node.TryReset(timeStamp)) {
                // Memoization
                return node.dist;
            }
            for (int i = 0; i < node.node.requires.Count; i++) {
                // Find the optimal predecessor node
                node.UpdatePredecessor(i, FindPath(context, node.node.requires[i]));
            }
            return node.dist;
        }

        private NodeContext FindPath(ProducerGraphExecutionContext context, ProducerRequirement target) {
            if (context.TryGetOutput(target.Item2, target.Item1, out var input)) {
                // If this requirement is fulfilled by input, return null
                return INPUT_NODE;
            }
            if (!producerLookup.TryGetValue(target, out var producers)) {
                // If we cannot find a producer for this requirement, return null
                return null;
            }
            var ret = producers.MinBy(p => FindPath(context, p));
            return ret.IsValid ? ret : null;
        }

        public IEnumerable<NodeContext> FindPath(ProducerGraphExecutionContext context, Type type, string id) {
            var key = (context, id, type);
            if (pathCache.TryGetValue(key, out var list)) {
                // Cache hit
                return list;
            }
            // Memoization search
            var target = (id, type);
            timeStamp.Increase();
            var node = FindPath(context, target);
            if (node == null) {
                throw new ArgumentException($"Cannot find a path to produce {type.Name}#{id}");
            }
            list = node.GetPath(timeStamp.Next).ToArray();
            pathCache[key] = list;
            return list;
        }
    }
}