using HoshinoSharp.Hoshino.Message;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

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

        public T GetResolver<T>() where T : ResolverBase {
            return Resolvers.Find(resolver => resolver is T) as T;
        }

        #region Generators
        private static readonly List<string> SKIP_OPTIONS = new List<string> {
            "s", "skip", "跳过"
        };

        private IEnumerable<ResolverBase> OnDrawTileResolvers() {
            if (TryGetResolver<PlayTileResolver>(out var resolver1)) {
                yield return resolver1;
            }
            if (TryGetResolver<RiichiResolver>(out var resolver2)) {
                yield return resolver2;
            }
            if (TryGetResolver<RonResolver>(out var resolver3)) {
                yield return resolver3;
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

        public async Task OnDrawTile(Hand hand, GameTile incoming) {
            var actions = new PlayerActions();
            foreach (var resolver in OnDrawTileResolvers()) {
                if (resolver.ResolveAction(hand, incoming, out var output)) {
                    actions.AddRange(output);
                }
            }
            Debug.Assert(actions.Count > 0);
            var msg = $"{hand.hand} +{incoming}\n{actions.GetMessage(hand.player)}";
            await hand.game.SendPrivate(hand.player, msg);
            hand.game.RegisterListener(actions);
        }

        public async Task OnDiscardTile(Hand hand, GameTile discard) {
            var actions = new PlayerActions();
            var resolvers = OnDiscardTileResolvers().ToArray();
            var currentPlayer = hand.game.GetPlayer(hand.player);
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
                        player = player.id,
                        options = SKIP_OPTIONS,
                        msg = new HMessage($"s：跳过"),
                        trigger = (_) => {
                            // TODO(Frenqy)
                        }
                    });
                    var msg = $"{player.hand.hand} +{discard} ({currentPlayer.nickname})\n{actions.GetMessage(player.id)}";
                    await hand.game.SendPrivate(player.id, msg);
                }
            }
            hand.game.RegisterListener(actions);
        }
    }
}
