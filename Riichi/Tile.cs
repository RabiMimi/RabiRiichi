using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RabiRiichi.Riichi {
    public enum TileSuit : byte {
        Invalid, M, P, S, Z
    }

    public struct Tile : IComparable<Tile> {
        public static readonly Tile Empty = new(0);
        /// <summary>
        /// LSB to MSB:
        /// 4 bit digit
        /// 3 bit group
        /// 1 bit akadora
        /// </summary>
        public byte Val { get; private set; }

        /// <summary> 不考虑赤宝的值。值一样说明是相同牌 </summary>
        public byte NoDoraVal => (byte)(Val & 0x7f);
        /// <summary> 去掉赤宝标记后的牌 </summary>
        public Tile WithoutDora => new(NoDoraVal);

        /// <summary> 点数 </summary>
        public byte Num {
            get => (byte)(Val & 0x0f);
            set => Val = (byte)((Val & ~0x0f) | value);
        }

        /// <summary> 种类 </summary>
        public TileSuit Suit {
            get => (TileSuit)((Val >> 4) & 0x07);
            set => Val = (byte)((Val & ~0x70) | ((byte)value << 4));
        }

        /// <summary> 是否是赤宝牌 </summary>
        public bool Akadora {
            get => (Val & 0x80) != 0;
            set {
                if (value)
                    Val |= 0x80;
                else
                    Val &= 0x7f;
            }
        }

        /// <summary> 是否是合法牌 </summary>
        public bool IsValid {
            get {
                if (Suit == TileSuit.Invalid || Suit > TileSuit.Z)
                    return false;
                if (Num < 1 || Num > 9)
                    return false;
                if (IsZ && Num > 7)
                    return false;
                return true;
            }
        }

        /// <summary> 是否是空牌（最常见的非法牌，一般用于表示牌背） </summary>
        public bool IsEmpty => this == Empty;

        /// <summary> 是否是19牌或字牌 </summary>
        public bool Is19Z => (IsMPS && (Num == 1 || Num == 9)) || IsZ;

        /// <summary> 是否是字牌 </summary>
        public bool IsZ => Suit == TileSuit.Z;

        /// <summary> 是否是万筒索 </summary>
        public bool IsMPS => Suit == TileSuit.M || Suit == TileSuit.P || Suit == TileSuit.S;

        /// <summary> 是否是三元牌 </summary>
        public bool IsSangen => IsZ && Num >= 5 && Num <= 7;

        /// <summary> 是否是风牌 </summary>
        public bool IsWind => IsZ && Num >= 1 && Num <= 4;

        public Tile(byte val = 0) { Val = val; }
        public Tile(TileSuit gr, int num, bool isAkadora = false) {
            Val = 0;
            Suit = gr;
            Num = (byte)num;
            Akadora = isAkadora;
        }
        public static Tile From(Wind wind) => new(TileSuit.Z, (int)wind + 1);

        public Tile(string str) {
            Val = 0;
            string original = str;
            if (str.StartsWith("r")) {
                Akadora = true;
                str = str[1..];
            }
            if (str.Length != 2) {
                ThrowInvalidArgument(original);
            }
            char num = str[0];
            if (num < '0' || num > '9') {
                ThrowInvalidArgument(original);
            }
            Num = (byte)(num - '0');
            Suit = str[1].ToGroup();
            if (!IsValid) {
                ThrowInvalidArgument(original);
            }
        }

        public override string ToString() {
            var builder = new StringBuilder();
            if (Akadora)
                builder.Append('r');
            builder.Append(Num);
            builder.Append(Suit.ToChar());
            return builder.ToString();
        }

        public override bool Equals(object obj) {
            if (obj is Tile rhs) {
                return this == rhs;
            }
            return false;
        }

        public override int GetHashCode() {
            return Val;
        }

        public int CompareTo(Tile other) {
            int result = Suit.CompareTo(other.Suit);
            if (result != 0)
                return result;
            result = Num.CompareTo(other.Num);
            if (result != 0)
                return result;
            return Akadora.CompareTo(other.Akadora);
        }

        /// <summary> 是否是相同的牌，赤dora视为相同 </summary>
        public bool IsSame(Tile other) => Suit == other.Suit && Num == other.Num;
        /// <summary> 是否是下一张牌，用于顺子计算 </summary>
        public bool IsNext(Tile other) => IsMPS && Suit == other.Suit && other.Num == Num + 1;
        /// <summary> 是否是上一张牌，用于顺子计算 </summary>
        public bool IsPrev(Tile other) => IsMPS && Suit == other.Suit && other.Num == Num - 1;
        /// <summary> 上一张牌，用于顺子计算 </summary>
        public Tile Prev => IsMPS ? new Tile {
            Num = (byte)(Num - 1),
            Suit = Suit,
            Akadora = false
        } : Empty;
        /// <summary> 下一张牌，用于顺子计算 </summary>
        public Tile Next => IsMPS ? new Tile {
            Num = (byte)(Num + 1),
            Suit = Suit,
            Akadora = false
        } : Empty;

        /// <summary> 下一张牌，用于宝牌指示牌计算 </summary>
        public Tile NextDora {
            get {
                if (IsMPS)
                    return new Tile { Num = (byte)(Num % 9 + 1), Suit = Suit };
                if (IsZ) {
                    if (Num <= 4)
                        return new Tile { Num = (byte)(Num % 4 + 1), Suit = Suit };
                    byte newNum = (byte)(Num == 7 ? 5 : Num + 1);
                    return new Tile { Num = newNum, Suit = Suit };
                }
                return Empty;
            }
        }

        public static implicit operator byte(Tile t) => t.Val;
        private static void ThrowInvalidArgument(string arg) {
            throw new ArgumentException("Invalid cast to tile: " + arg);
        }

        public static bool operator ==(Tile lhs, Tile rhs) {
            return lhs.Val == rhs.Val;
        }
        public static bool operator !=(Tile lhs, Tile rhs) {
            return lhs.Val != rhs.Val;
        }
    }

    /// <summary>
    /// 一堆牌。
    /// </summary>
    public class Tiles : List<Tile> {
        public static readonly Tiles Empty = new();
        public Tiles(IEnumerable<Tile> tiles) : base(tiles) { }
        public Tiles(string tiles = "") : base() {
            bool isDora = false;
            foreach (char c in tiles) {
                if (c >= '0' && c <= '9') {
                    byte num = (byte)(c - '0');
                    if (num == 0) {
                        num = 5;
                        isDora = true;
                    }
                    Add(new Tile {
                        Num = num,
                        Akadora = isDora,
                    });
                    isDora = false;
                    continue;
                }
                if (isDora) {
                    ThrowInvalidArgument(tiles);
                }
                if (c == 'r') {
                    isDora = true;
                    continue;
                }
                var g = c.ToGroup();
                if (g == TileSuit.Invalid) {
                    ThrowInvalidArgument(tiles);
                }
                for (int i = Count - 1; i >= 0; i--) {
                    var tile = this[i];
                    if (tile.Suit != TileSuit.Invalid) {
                        break;
                    }
                    tile.Suit = g;
                    this[i] = tile;
                }
            }
            if (this.Any(tile => !tile.IsValid)) {
                ThrowInvalidArgument(tiles);
            }
        }

        public void Remove(IEnumerable<Tile> tiles) {
            foreach (var tile in tiles) {
                Remove(tile);
            }
        }

        /// <summary>
        /// 求交集，赤宝不敏感
        /// </summary>
        /// <param name="tiles">不能包含赤宝</param>
        public IEnumerable<Tile> Intersect(Tiles tiles) {
            return this.Where(t => tiles.Contains(t.WithoutDora));
        }

        public override string ToString() {
            var builder = new StringBuilder();
            char group = '\0';
            foreach (var tile in this) {
                string str = tile.ToString();
                if (group != str[^1]) {
                    if (group != '\0') {
                        builder.Append(group);
                    }
                    group = str[^1];
                }
                builder.Append(str[0..^1]);
            }
            if (group != '\0') {
                builder.Append(group);
            }
            return builder.ToString();
        }

        private static void ThrowInvalidArgument(string arg) {
            throw new ArgumentException("Invalid tiles: " + arg);
        }

        public bool IsKou => Count == 3
            && this[0].IsSame(this[1]) && this[1].IsSame(this[2]);
        public bool IsKan {
            get {
                if (Count != 4)
                    return false;
                for (int i = 1; i < Count; i++) {
                    if (!this[i - 1].IsSame(this[i]))
                        return false;
                }
                return true;
            }
        }
        public bool IsShun {
            get {
                if (Count != 3)
                    return false;
                var list = this.ToList();
                list.Sort();
                return list[0].IsNext(list[1]) && list[1].IsNext(list[2]);
            }
        }
        public bool IsJan => Count == 2 && this[0].IsSame(this[1]);

        /// <summary> 所有牌 </summary>
        public static Tiles All {
            get {
                var ret = new Tiles();
                for (var g = TileSuit.M; g <= TileSuit.Z; g++) {
                    int maxTile = g == TileSuit.Z ? 7 : 9;
                    for (int i = 1; i <= maxTile; i++) {
                        for (int j = 0; j < 4; j++) {
                            ret.Add(new Tile {
                                Suit = g,
                                Num = (byte)i,
                                Akadora = maxTile == 9 && i == 5 && j == 0
                            });
                        }
                    }
                }
                return ret;
            }
        }

        /// <summary> 所有牌（不重复，无赤宝） </summary>
        public static Tiles AllDistinct => new("123456789m123456789p123456789s1234567z");

        /// <summary> 所有19牌和字牌 </summary>
        public static Tiles T19Z => new("19m19p19s1234567z");

        /// <summary> 所有19牌 </summary>
        public static Tiles T19 => new("19m19p19s");
    }
}
