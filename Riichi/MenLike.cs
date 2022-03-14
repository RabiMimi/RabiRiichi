using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Riichi {

    /// <summary> 面子或雀头或单张 </summary>
    public abstract class MenLike : GameTiles {
        /// <summary> 唯一确定该面子的值（忽略赤宝） </summary>
        public ulong Value { get; protected set; }

        /// <summary> 明暗 </summary>
        public bool IsClose { get; protected set; }

        /// <summary> 花色 </summary>
        public TileSuit Suit => First.tile.Suit;

        /// <summary> 第一张牌 </summary>
        public GameTile First => this[0];

        private void Init() {
            Sort();
            Value = 0;
            foreach (var tile in this) {
                Value = (Value << 8) | tile.tile.NoDoraVal;
            }
            IsClose = this.All(t => t.IsTsumo);
        }

        public MenLike() { }
        public MenLike(IEnumerable<GameTile> tiles) : base(tiles) {
            Init();
        }
        public MenLike(IEnumerable<Tile> tiles) : base(tiles) {
            Init();
        }

        public static bool IsShun(List<GameTile> tiles) {
            if (tiles.Count != 3)
                return false;
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
            if (tiles.Count != 4)
                return false;
            return IsKou(tiles, true);
        }

        public static bool IsMusou(List<GameTile> tiles) {
            return tiles.Count == 1;
        }

        /// <summary> 根据牌返回最适合的类 </summary>
        public static MenLike From(List<GameTile> tiles) {
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
        public static MenLike From(List<Tile> tiles) {
            return From(new GameTiles(tiles));
        }
    }

    /// <summary> 顺子 </summary>
    public class Shun : MenLike {
        public Shun(IEnumerable<Tile> tiles) : base(tiles) {
            Logger.Assert(IsShun(this), "顺子必须是顺子");
        }
        public Shun(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsShun(this), "顺子必须是顺子");
        }

        public override bool IsSame(GameTiles other) {
            if (other is not Shun shun)
                return false;
            return First.IsSame(shun.First);
        }
    }

    /// <summary> 刻子 </summary>
    public class Kou : MenLike {
        public Kou(IEnumerable<Tile> tiles) : base(tiles) {
            Logger.Assert(IsKou(this), "刻子必须是刻子");
        }
        public Kou(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsKou(this), "刻子必须是刻子");
        }

        /// <summary> 判定是否相同，赤宝牌视为相同牌，杠和刻视为相同 </summary>
        public override bool IsSame(GameTiles other) {
            if (other is not (Kou or Kan))
                return false;
            return First.IsSame((other as MenLike).First);
        }
    }

    /// <summary> 杠子 </summary>
    public class Kan : MenLike {

        /// <summary> 是暗杠/明杠/加杠 </summary>
        public TileSource KanSource { get; init; }
        public Kan(IEnumerable<Tile> tiles, TileSource kanSource) : base(tiles) {
            Logger.Assert(IsKan(this), "杠子必须是杠子");
            KanSource = kanSource;
        }
        public Kan(IEnumerable<GameTile> tiles, TileSource? kanSource = null) : base(tiles) {
            Logger.Assert(IsKan(this), "杠子必须是杠子");
            if (!kanSource.HasValue) {
                // 检查是哪种杠
                if (IsClose) {
                    kanSource = TileSource.AnKan;
                } else if (tiles.Any(t => t.formTime != tiles.First().formTime)) {
                    kanSource = TileSource.KaKan;
                } else {
                    kanSource = TileSource.DaiMinKan;
                }
            }
            KanSource = kanSource.Value;
        }

        /// <summary> 判定是否相同，赤宝牌视为相同牌，杠和刻视为相同 </summary>
        public override bool IsSame(GameTiles other) {
            if (other is not (Kou or Kan))
                return false;
            return First.IsSame((other as MenLike).First);
        }
    }

    /// <summary> 雀头 </summary>
    public class Jantou : MenLike {
        public Jantou(IEnumerable<Tile> tiles) : base(tiles) {
            Logger.Assert(IsJan(this), "雀头必须是雀头");
        }
        public Jantou(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsJan(this), "雀头必须是雀头");
        }

        public override bool IsSame(GameTiles other) {
            if (other is not Jantou jantou)
                return false;
            return First.IsSame(jantou.First);
        }
    }

    /// <summary> 单牌，仅用于国士无双 </summary>
    public class Musou : MenLike {
        public Musou(IEnumerable<Tile> tiles) : base(tiles) {
            Logger.Assert(IsMusou(this), "单牌必须是单牌");
        }
        public Musou(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsMusou(this), "单牌必须是单牌");
        }

        public override bool IsSame(GameTiles other) {
            if (other is not Musou musou)
                return false;
            return First.IsSame(musou.First);
        }
    }
}