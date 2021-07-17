using HoshinoSharp.Hoshino.Message;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using HUtil = HoshinoSharp.Runtime.Util;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否可以立直
    /// </summary>
    public class RiichiResolver : ResolverBase {
        public readonly List<BasePattern> basePatterns = new List<BasePattern>();

        public void RegisterBasePattern(BasePattern pattern) {
            basePatterns.Add(pattern);
        }

        public override bool ResolveAction(Hand hand, GameTile incoming, out PlayerActions output) {
            if (hand.riichi || !hand.menzen) {
                output = null;
                return false;
            }
            // TODO(Frenqy)
            output = null;
            return false;
        }
    }
}
