using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Resolver {
    public class ActionManager {
        public static ResolverBase[] resolvers = new ResolverBase[] {
            new RonResolver(),
            new RiichiResolver(),
            new ChiResolver(),
            new KanResolver(),
            new PonResolver(),
            new PlayTileResolver(),
        };

        public readonly List<ResolverBase> Resolvers = new List<ResolverBase>();

        public void RegisterResolver(ResolverBase resolver) {
            Resolvers.Add(resolver);
        }

        public bool TryGetResolver<T>(out T ret) where T: ResolverBase {
            ret = Resolvers.Find(resolver => resolver is T) as T;
            return ret != null;
        }

        public T GetResolver<T>() where T : ResolverBase {
            return Resolvers.Find(resolver => resolver is T) as T;
        }

        #region Generators
        private IEnumerable<ResolverBase> OnDrawTileResolvers(bool selectOnly) {
            if (TryGetResolver<PlayTileResolver>(out var resolver1)) {
                yield return resolver1;
            }
            if (selectOnly) {
                yield break;
            }
            if (TryGetResolver<RiichiResolver>(out var resolver2)) {
                yield return resolver2;
            }
            if (TryGetResolver<RonResolver>(out var resolver3)) {
                yield return resolver3;
            }
            if (TryGetResolver<KanResolver>(out var resolver4)) {
                yield return resolver4;
            }
        }

        private IEnumerable<ResolverBase> OnDiscardTileResolvers() {
            if (TryGetResolver<ChiResolver>(out var resolver1)) {
                yield return resolver1;
            }
            if (TryGetResolver<PonResolver>(out var resolver2)) {
                yield return resolver2;
            }
            if (TryGetResolver<KanResolver>(out var resolver3)) {
                yield return resolver3;
            }
            if (TryGetResolver<RonResolver>(out var resolver4)) {
                yield return resolver4;
            }
        }

        #endregion
        // TODO: Use event driven logic
/*
        public async Task OnDrawTile(Hand hand, GameTile incoming, bool selectOnly) {
            var actions = new PlayerActions();
            foreach (var resolver in OnDrawTileResolvers(selectOnly)) {
                if (resolver.ResolveAction(hand, incoming, out var output)) {
                    actions.AddRange(output);
                }
            }
            Debug.Assert(actions.Count > 0);
            bool forceAction = actions.Count == 1 && actions[0].options.Count == 1;
            if (forceAction) {
                actions[0].choice = 0;
                actions[0].trigger(actions[0]);
            } else {
                string strIncoming = incoming == null ? "" : $" +{incoming}";
                // var msg = $"{hand.hand}{strIncoming}\n{actions.GetMessage(hand.player)}";
                // Send message
            }
        }

        public async Task<bool> OnDiscardTile(Hand hand, GameTile discard) {
            var actions = new PlayerActions();
            var resolvers = OnDiscardTileResolvers().ToArray();
            var currentPlayer = hand.player;
            foreach (var player in hand.game.players) {
                bool suc = false;
                foreach (var resolver in resolvers) {
                    if (resolver.ResolveAction(player.hand, discard, out var output)) {
                        suc = true;
                        actions.AddRange(output);
                    }
                }
                if (suc) {
                    actions.Add(new PlayerAction {
                        priority = PlayerAction.Priority.SKIP,
                        player = player,
                        options = SKIP_OPTIONS,
                        trigger = (_) => {
                            // TODO(Frenqy)
                        }
                    });
                    // var msg = $"{player.hand.hand} +{discard} ({currentPlayer.id})\n{actions.GetMessage(player.id)}";
                    // Send message
                }
            }
            if (actions.Count > 0) {
                return true;
            } else {
                return false;
            }
        }
*/
    }
}
