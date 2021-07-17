using System.Collections.Generic;

namespace RabiRiichi.Resolver {
    public class ActionManager {
        public readonly List<ResolverBase> Resolvers;

        public void RegisterResolver(ResolverBase resolver) {
            Resolvers.Add(resolver);
        }

        public bool TryGetResolver<T>(out T ret) where T: ResolverBase {
            ret = Resolvers.Find(resolver => resolver is T) as T;
            return ret != null;
        }
    }
}
