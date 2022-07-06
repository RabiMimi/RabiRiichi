using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ProducerRequirement = System.ValueTuple<string, System.Type>;

namespace RabiRiichi.Util.Graphs {
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
        public readonly List<ProducerRequirement> inputs = new();
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
                } else if (param.GetCustomAttribute<InputAttribute>() is InputAttribute iAttr) {
                    inputs.Add((iAttr.Id, param.ParameterType));
                }
            }
        }
    }

    public class ProducerGraphExecutionContext {
        private class NodeContext {
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

        private readonly Dictionary<string, List<object>> inputs = new();
        private readonly Dictionary<string, List<object>> produces = new();
        private readonly Dictionary<ProducerRequirement, List<NodeContext>> edges = new();
        private readonly List<NodeContext> nodes = new();

        private static bool TryGet(Dictionary<string, List<object>> dict, Type type, string id, out object obj) {
            if (!dict.TryGetValue(id, out var list)) {
                obj = null;
                return false;
            }
            obj = list.FirstOrDefault(x => x.GetType().IsAssignableTo(type));
            return obj != null;
        }

        private void AddProduces(string id, object obj) {
            if (!produces.TryGetValue(id, out var list)) {
                list = new List<object>();
                produces[id] = list;
            }
            list.Add(obj);
        }

        private IEnumerable<NodeContext> FindRoute(Type type, string id) {
            var queue = new PriorityQueue<NodeContext, int>();
            var visited = new HashSet<NodeContext>();
            nodes.RemoveAll(ctx => ctx.node.inputs.Any(i => !TryGetInput(i.Item2, i.Item1, out _)));
            // Init
            foreach (var node in nodes.Where(
                n => n.node.requires.All(x => TryGetOutput(x.Item2, x.Item1, out _)))) {
                node.dist = node.node.cost;
                queue.Enqueue(node, node.dist);
            }
            // Shortest path
            while (queue.Count > 0) {
                var node = queue.Dequeue();
                if (!visited.Add(node)) {
                    continue;
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
            try {
                var target = nodes.Where(n => n.node.produces.Satisfies((id, type))).MinBy(n => n.dist);
                visited.Clear();
                return target?.GetRoute(visited);
            } catch (ArgumentException) {
                return null;
            }
        }

        public ProducerGraphExecutionContext(ProducerGraph graph) {
            inputs[""] = new List<object> { graph };
            foreach (var node in graph.nodes) {
                nodes.Add(new NodeContext(node));
            }
            foreach (var node in nodes) {
                foreach (var require in node.node.requires) {
                    if (!edges.TryGetValue(require, out var list)) {
                        list = new List<NodeContext>();
                        edges[require] = list;
                    }
                    list.Add(node);
                }
            }
        }

        public bool TryGetInput(Type type, string id, out object obj)
            => TryGet(inputs, type, id, out obj);

        public bool TryGetOutput(Type type, string id, out object obj)
            => TryGet(produces, type, id, out obj);

        public ProducerGraphExecutionContext SetInput(string id, object obj) {
            if (!inputs.TryGetValue(id, out var list)) {
                list = new List<object>();
                inputs[id] = list;
            }
            list.Add(obj);
            return this;
        }

        public T Execute<T>(string id = "") {
            try {
                return ExecuteAsync<T>(id).Result;
            } catch (AggregateException e) {
                throw e.InnerException;
            }
        }

        public async Task<T> ExecuteAsync<T>(string id = "") {
            var route = FindRoute(typeof(T), id);
            if (route == null) {
                throw new ArgumentException($"No route to produce {typeof(T)} with id {id}");
            }
            foreach (var node in route) {
                var parameters = node.node.method.GetParameters().Select(p => {
                    if (p.GetCustomAttribute<InputAttribute>() is InputAttribute iAttr) {
                        Logger.Assert(TryGetInput(p.ParameterType, iAttr.Id, out var obj),
                            $"No input of type {p.ParameterType} with id {iAttr.Id}");
                        return obj;
                    }
                    if (p.GetCustomAttribute<ConsumesAttribute>() is ConsumesAttribute oAttr) {
                        Logger.Assert(TryGetOutput(p.ParameterType, oAttr.Id, out var obj),
                            $"No one produces type {p.ParameterType} with id {oAttr.Id}");
                        return obj;
                    }
                    throw new ArgumentException($"No input or consumes attribute for parameter {p.Name}");
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
    }

    public class ProducerGraph {
        public readonly List<ProducerGraphNode> nodes = new();

        public ProducerGraph Register(object obj, MethodInfo[] methods) {
            foreach (var method in methods) {
                if (method.GetCustomAttribute<ProducesAttribute>() == null) {
                    continue;
                }
                var invalidParam = method.GetParameters().FirstOrDefault(
                    p => p.GetCustomAttribute<InputAttribute>() == null
                        == (p.GetCustomAttribute<ConsumesAttribute>() == null));
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

        public ProducerGraphExecutionContext Build() => new(this);
    }
}