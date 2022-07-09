using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                throw e.InnerException;
            }
        }

        public async Task<T> ExecuteAsync<T>(string id = "") {
            var route = graph.FindRoute(this, typeof(T), id);
            if (route == null) {
                throw new ArgumentException($"No route to produce {typeof(T)} with id {id}");
            }
            foreach (var node in route) {
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
            public readonly ProducerGraphNode node;
            public readonly NodeContext[] predecessors;
            public int dist = INIT_COST;

            public bool UpdateDist() {
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

            public IEnumerable<NodeContext> GetRoute(HashSet<NodeContext> visited) {
                if (!visited.Add(this)) {
                    yield break;
                }
                foreach (var pred in predecessors) {
                    if (pred != null) {
                        foreach (var route in pred.GetRoute(visited)) {
                            yield return route;
                        }
                    }
                }
                yield return this;
            }

            public NodeContext(ProducerGraphNode node) {
                this.node = node;
                predecessors = new NodeContext[node.requires.Count];
            }
        }

        public readonly List<ProducerGraphNode> nodes = new();

        private readonly Dictionary<ProducerRequirement, List<NodeContext>> edges = new();
        private readonly List<NodeContext> nodeCtxs = new();
        private bool isFinalized = false;
        public readonly Dictionary<(ProducerGraphExecutionContext, string, Type), List<NodeContext>> routeCache = new();
        private readonly PriorityQueue<NodeContext, int> queue = new();
        private readonly HashSet<NodeContext> visited = new();

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
                nodes.Add(node);
            }
            return this;
        }

        public ProducerGraph Register(object obj)
            => Register(obj, obj.GetType().GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        public ProducerGraph Register<T>() where T : class
            => Register(null, typeof(T).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));

        private void FinalizeNodes() {
            if (isFinalized) {
                return;
            }
            foreach (var node in nodes) {
                var ctx = new NodeContext(node);
                nodeCtxs.Add(ctx);
                foreach (var require in node.requires) {
                    if (!edges.TryGetValue(require, out var list)) {
                        list = new List<NodeContext>();
                        edges[require] = list;
                    }
                    list.Add(ctx);
                }
            }
            isFinalized = true;
        }

        public ProducerGraphExecutionContext Build() {
            FinalizeNodes();
            return new(this);
        }

        public List<NodeContext> FindRoute(ProducerGraphExecutionContext context, Type type, string id) {
            var key = (context, id, type);
            if (routeCache.TryGetValue(key, out var list)) {
                return list;
            }
            // Init
            queue.Clear();
            visited.Clear();
            foreach (var ctx in nodeCtxs) {
                ctx.dist = NodeContext.INIT_COST;
                Array.Fill(ctx.predecessors, null);
            }
            foreach (var ctx in nodeCtxs.Where(
                n => n.node.requires.All(x => context.TryGetOutput(x.Item2, x.Item1, out _)))) {
                ctx.dist = ctx.node.cost;
                queue.Enqueue(ctx, ctx.dist);
            }
            // Shortest path
            NodeContext target = null;
            while (queue.Count > 0) {
                var node = queue.Dequeue();
                if (!visited.Add(node)) {
                    continue;
                }
                if (node.node.produces.Satisfies((id, type))) {
                    target = node;
                    break;
                }
                if (!edges.TryGetValue(node.node.produces, out var edgesList)) {
                    continue;
                }
                foreach (var next in edgesList) {
                    int idx = next.node.IndexOfRequirement(node.node.produces);
                    if (next.UpdatePredecessor(idx, node)) {
                        queue.Enqueue(next, next.dist);
                    }
                }
            }
            visited.Clear();
            list = target?.GetRoute(visited)?.ToList();
            routeCache[key] = list;
            return list;
        }
    }
}