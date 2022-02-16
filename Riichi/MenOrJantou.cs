using System;
using System.Linq;
using System.Collections.Generic;
using RabiRiichi.Util;

namespace RabiRiichi.Riichi {

    /// <summary> 面子或雀头 </summary>
    public abstract class MenOrJantou : GameTiles {
        /// <summary> 无赤宝的代表牌，对于顺子来说是第一张 </summary>
        public Tile Value { get; protected set; }

        /// <summary> 明暗 </summary>
        public bool IsClose { get; protected set; }

        public MenOrJantou() { }
        public MenOrJantou(IEnumerable<GameTile> tiles) : base(tiles) {
            Sort();
            Value = this[0].tile.WithoutDora;
            IsClose = this.All(t => t.IsTsumo);
        }
        public MenOrJantou(IEnumerable<Tile> tiles) : base(tiles) {
            Sort();
            Value = this[0].tile.WithoutDora;
            IsClose = this.All(t => t.IsTsumo);
        }

        public static bool IsShun(List<GameTile> tiles) {
            if (tiles.Count != 3) return false;
            tiles.Sort();
            return tiles[0].NextIs(tiles[1]) && tiles[1].NextIs(tiles[2]);
        }

        public static bool IsKou(List<GameTile> tiles, bool allowKan = false) {
            if (tiles.Count != 3 && tiles.Count != 4) {
                return false;
            }
            if (!allowKan && tiles.Count == 4) {
                return false;
            }
            for (int i = 1; i < tiles.Count; i++) {
                if (!tiles[i - 1].IsSame(tiles[i])) {
                    return false;
                }
            }
            return true;
        }

        public static bool IsJan(List<GameTile> tiles) {
            return tiles.Count == 2 && tiles[0].IsSame(tiles[1]);
        }

        public static bool IsKan(List<GameTile> tiles) {
            if (tiles.Count != 4) return false;
            return IsKou(tiles, true);
        }

        public static bool IsMusou(List<GameTile> tiles) {
            return tiles.Count == 1;
        }

        /// <summary> 根据牌返回最适合的类 </summary>
        public static MenOrJantou From(List<GameTile> tiles) {
            if (IsJan(tiles)) {
                return new Jantou(tiles);
            } else if (IsKan(tiles)) {
                return new Kan(tiles);
            } else if (IsKou(tiles)) {
                return new Kou(tiles);
            } else if (IsShun(tiles)) {
                return new Shun(tiles);
            } else if (IsMusou(tiles)) {
                return new Musou(tiles);
            } else {
                throw new ArgumentException("不是合法的面子或雀头");
            }
        }

        /// <summary> 根据牌返回最适合的类 </summary>
        public static MenOrJantou From(List<Tile> tiles) {
            return From(new GameTiles(tiles));
        }
    }

    /// <summary> 顺子 </summary>
    public class Shun : MenOrJantou {
        public Shun(IEnumerable<Tile> tiles) : base(tiles) {
            Logger.Assert(IsShun(this), "顺子必须是顺子");
        }
        public Shun(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsShun(this), "顺子必须是顺子");
        }

        public override bool IsSame(GameTiles other) {
            if (!(other is Shun))
                return false;
            return this[0].IsSame(other[0]);
        }
    }

    /// <summary> 刻子 </summary>
    public class Kou : MenOrJantou {
        public Kou(IEnumerable<Tile> tiles) : base(tiles) {
            Logger.Assert(IsKou(this), "刻子必须是刻子");
        }
        public Kou(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsKou(this), "刻子必须是刻子");
        }

        /// <summary> 判定是否相同，赤宝牌视为相同牌，杠和刻视为相同 </summary>
        public override bool IsSame(GameTiles other) {
            if (!(other is Kou) && !(other is Kan))
                return false;
            return this[0].IsSame(other[0]);
        }
    }

    /// <summary> 杠子 </summary>
    public class Kan : MenOrJantou {
        public Kan(IEnumerable<Tile> tiles) : base(tiles) {
            Logger.Assert(IsKan(this), "杠子必须是杠子");
        }
        public Kan(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsKan(this), "杠子必须是杠子");
        }

        /// <summary> 判定是否相同，赤宝牌视为相同牌，杠和刻视为相同 </summary>
        public override bool IsSame(GameTiles other) {
            if (!(other is Kan) && !(other is Kou))
                return false;
            return this[0].IsSame(other[0]);
        }
    }

    /// <summary> 雀头 </summary>
    public class Jantou : MenOrJantou {
        public Jantou(IEnumerable<Tile> tiles) : base(tiles) {
            Logger.Assert(IsJan(this), "雀头必须是雀头");
        }
        public Jantou(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsJan(this), "雀头必须是雀头");
        }

        public override bool IsSame(GameTiles other) {
            if (!(other is Jantou))
                return false;
            return this[0].IsSame(other[0]);
        }
    }

    /// <summary> 单牌，仅用于国士无双 </summary>
    public class Musou : MenOrJantou {
        public Musou(IEnumerable<Tile> tiles) : base(tiles) {
            Logger.Assert(IsMusou(this), "单牌必须是单牌");
        }
        public Musou(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsMusou(this), "单牌必须是单牌");
        }

        public override bool IsSame(GameTiles other) {
            if (!(other is Musou))
                return false;
            return this[0].IsSame(other[0]);
        }
    }
}