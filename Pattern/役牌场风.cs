﻿using System;
using System.Collections.Generic;
using System.Text;
using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern {
    public class 役牌场风 : 役牌 {
        protected override Tile YakuTile => yakuTile;
        private Tile yakuTile;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            yakuTile = Tile.From(hand.game.gameInfo.wind);
            return base.Resolve(groups, hand, incoming, scorings);
        }
    }
}
