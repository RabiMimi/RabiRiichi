using RabiRiichi.Communication.Proto;
using RabiRiichi.Generated.Core;
using RabiRiichi.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RabiRiichi.Core {

    /// <summary> 面子或雀头或单张 </summary>
    public abstract class MenLike : ReadOnlyCollection<GameTile> {
        /// <summary> 唯一确定该面子的值（忽略赤宝） </summary>
        public readonly ulong Value;

        /// <summary> 明暗 </summary>
        public readonly bool IsClose;

        /// <summary> 花色 </summary>
        public TileSuit Suit => First.tile.Suit;

        /// <summary> 第一张牌 </summary>
        public GameTile First => this[0];

        public MenLike(IEnumerable<GameTile> tiles) : base(tiles.OrderBy(t => t.tile).ToList()) {
            Value = 0;
            foreach (var tile in this) {
                Value = (Value << 8) | tile.tile.NoDoraVal;
            }
            IsClose = this.All(t => t.IsTsumo);
        }

        public abstract bool IsSame(MenLike other);

        /// <summary> 判定是否有给出的牌，赤宝牌视为相同牌 </summary>
        public bool Contains(Tile tile) {
            return this.Any(t => t.tile.IsSame(tile));
        }

        public GameTile Find(Func<GameTile, bool> predicate) {
            return this.FirstOrDefault(predicate);
        }

        public Tiles ToTiles() {
            return new Tiles(this.Select(gameTile => gameTile.tile));
        }

        public static bool IsShun(ReadOnlyCollection<GameTile> tiles) {
            if (tiles.Count != 3)
                return false;
            var sorted = tiles.OrderBy(t => t.tile).ToList();
            return sorted[0].NextIs(sorted[1]) && sorted[1].NextIs(sorted[2]);
        }

        public static bool IsKou(ReadOnlyCollection<GameTile> tiles, bool allowKan = false) {
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

        public static bool IsJan(ReadOnlyCollection<GameTile> tiles) {
            return tiles.Count == 2 && tiles[0].IsSame(tiles[1]);
        }

        public static bool IsKan(ReadOnlyCollection<GameTile> tiles) {
            if (tiles.Count != 4)
                return false;
            return IsKou(tiles, true);
        }

        public static bool IsMusou(ReadOnlyCollection<GameTile> tiles) {
            return tiles.Count == 1;
        }

        /// <summary> 根据牌返回最适合的类 </summary>
        public static MenLike From(ReadOnlyCollection<GameTile> tiles) {
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
        public static MenLike From(IEnumerable<GameTile> tiles) {
            return From(tiles.ToList().AsReadOnly());
        }

        public MenLikeMsg ToProto() {
            var msg = new MenLikeMsg();
            msg.Tiles.AddRange(this.Select(ProtoConverters.ConvertGameTile));
            return msg;
        }
    }

    /// <summary> 顺子 </summary>
    public class Shun : MenLike {
        public Shun(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsShun(this), "顺子必须是顺子");
        }

        public override bool IsSame(MenLike other) {
            if (other is not Shun shun)
                return false;
            return First.IsSame(shun.First);
        }
    }

    /// <summary> 刻子 </summary>
    public class Kou : MenLike {
        public Kou(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsKou(this), "刻子必须是刻子");
        }

        /// <summary> 判定是否相同，赤宝牌视为相同牌，杠和刻视为相同 </summary>
        public override bool IsSame(MenLike other) {
            if (other is not (Kou or Kan))
                return false;
            return First.IsSame(other.First);
        }
    }

    /// <summary> 杠子 </summary>
    public class Kan : MenLike {

        /// <summary> 是暗杠/明杠/加杠 </summary>
        public TileSource KanSource { get; init; }
        public Kan(IEnumerable<GameTile> tiles, TileSource? kanSource = null) : base(tiles) {
            Logger.Assert(IsKan(this), "杠子必须是杠子");
            if (!kanSource.HasValue) {
                // 检查是哪种杠
                if (IsClose) {
                    kanSource = TileSource.Ankan;
                } else if (tiles.Any(t => t.formTime != tiles.First().formTime)) {
                    kanSource = TileSource.Kakan;
                } else {
                    kanSource = TileSource.Daiminkan;
                }
            }
            KanSource = kanSource.Value;
        }

        /// <summary> 判定是否相同，赤宝牌视为相同牌，杠和刻视为相同 </summary>
        public override bool IsSame(MenLike other) {
            if (other is not (Kou or Kan))
                return false;
            return First.IsSame(other.First);
        }
    }

    /// <summary> 雀头 </summary>
    public class Jantou : MenLike {
        public Jantou(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsJan(this), "雀头必须是雀头");
        }

        public override bool IsSame(MenLike other) {
            if (other is not Jantou jantou)
                return false;
            return First.IsSame(jantou.First);
        }
    }

    /// <summary> 单牌，仅用于国士无双 </summary>
    public class Musou : MenLike {
        public Musou(IEnumerable<GameTile> tiles) : base(tiles) {
            Logger.Assert(IsMusou(this), "单牌必须是单牌");
        }

        public override bool IsSame(MenLike other) {
            if (other is not Musou musou)
                return false;
            return First.IsSame(musou.First);
        }
    }
}