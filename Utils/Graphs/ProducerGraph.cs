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

        public int IndexOf(ProducerRequirement requirement) => requires.IndexOf(requirement);

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
            public readonly NodeContext[] predecessors;
            public readonly List<NodeContext> successors;
            public readonly ProducerGraphNode node;
            public int dist;

            public bool IsVisited(int time) => visitedTimeStamp == time;
            /// <summary> Set the node as visited. </summary>
            /// <param name="time">Current timestamp.</param>
            /// <returns>True if the node has not been visited before.</returns>
            public bool SetVisited(int time) {
                if (visitedTimeStamp == time) {
                    return false;
                }
                visitedTimeStamp = time;
                return true;
            }

            public bool IsInput => node == null;
            public bool IsValid => dist < INIT_COST;

            private bool UpdateDist() {
                int newDist = predecessors.Min(p => p.dist) + node.cost;
                if (newDist < dist) {
                    dist = newDist;
                    return true;
                }
                return false;
            }

            public bool UpdatePredecessor(int index, NodeContext predecessor) {
                if (predecessors[index].dist > predecessor.dist) {
                    predecessors[index] = predecessor;
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
                foreach (var path in predecessors.SelectMany(x => x.GetPath(timeStamp))) {
                    yield return path;
                }
                yield return this;
            }

            public NodeContext(ProducerGraphNode node, List<NodeContext> successors) {
                this.node = node;
                this.successors = successors;
                if (node != null) {
                    predecessors = new NodeContext[node.requires.Count];
                }
            }
        }

        /// <summary>
        /// A dummy node that tells a requirement is fulfilled by input.<br/>
        /// Never use its methods.
        /// </summary>
        private static readonly NodeContext INPUT_NODE = new(null, null) { dist = 0 };
        private static readonly NodeContext INVALID_NODE = new(null, null) { dist = NodeContext.INIT_COST };

        private readonly Dictionary<ProducerRequirement, List<NodeContext>> consumerLookup = new();
        private readonly List<NodeContext> nodeCtxs = new();
        private readonly Dictionary<(ProducerGraphExecutionContext, string, Type), NodeContext[]> pathCache = new();
        private AutoIncrementInt timeStamp = new();
        private readonly PriorityQueue<NodeContext, int> queue = new();

        private List<NodeContext> GetOrCreateConsumers(ProducerRequirement requires) {
            if (!consumerLookup.TryGetValue(requires, out var list)) {
                list = new List<NodeContext>();
                consumerLookup[requires] = list;
            }
            return list;
        }

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
                var node = new ProducerGraphNode(method.IsStatic ? null : obj, method);
                var nodeCtx = new NodeContext(node, GetOrCreateConsumers(node.produces));
                foreach (var require in node.requires) {
                    GetOrCreateConsumers(require).Add(nodeCtx);
                }
                nodeCtxs.Add(nodeCtx);
            }
            return this;
        }

        public ProducerGraph Register(object obj)
            => Register(obj, obj.GetType().GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        public ProducerGraph Register<T>() where T : class
            => Register(null, typeof(T).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));

        public ProducerGraphExecutionContext Build() => new(this);

        public IEnumerable<NodeContext> FindPath(ProducerGraphExecutionContext context, Type type, string id) {
            var key = (context, id, type);
            if (pathCache.TryGetValue(key, out var list)) {
                // Cache hit
                return list;
            }
            // BFS
            var output = (id, type);
            queue.Clear();
            timeStamp.Increase();
            foreach (var node in nodeCtxs) {
                bool allInputs = true;
                for (int i = 0; i < node.predecessors.Length; i++) {
                    var require = node.node.requires[i];
                    if (context.TryGetOutput(require.Item2, require.Item1, out _)) {
                        node.predecessors[i] = INPUT_NODE;
                    } else {
                        node.predecessors[i] = INVALID_NODE;
                        allInputs = false;
                    }
                }
                if (allInputs) {
                    node.dist = node.node.cost;
                    queue.Enqueue(node, node.dist);
                } else {
                    node.dist = NodeContext.INIT_COST;
                }
            }
            // Shortest path
            NodeContext target = null;
            while (queue.Count > 0) {
                var nodeCtx = queue.Dequeue();
                if (!nodeCtx.SetVisited(timeStamp)) {
                    continue;
                }
                if (nodeCtx.node.produces.Satisfies(output)) {
                    target = nodeCtx;
                    break;
                }
                foreach (var successor in nodeCtx.successors) {
                    if (successor.UpdatePredecessor(successor.node.IndexOf(nodeCtx.node.produces), nodeCtx)) {
                        queue.Enqueue(successor, successor.dist);
                    }
                }
            }
            if (target == null) {
                throw new ArgumentException($"Cannot find a path to produce {type.Name}#{id}");
            }
            list = target.GetPath(timeStamp.Next).ToArray();
            pathCache[key] = list;
            return list;
        }
    }
}