using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RabiRiichi.Riichi {
    public enum Group : byte {
        Invalid, M, P, S, Z
    }

    public struct Hai : IComparable<Hai> {
        public static Hai Empty = new Hai(0);
        /// <summary>
        /// LSB to MSB:
        /// 4 bit digit
        /// 3 bit group
        /// 1 bit akadora
        /// </summary>
        public byte Val { get; private set; }

        public byte Num {
            get => (byte)(Val & 0x0f);
            set => Val = (byte)((Val & ~0x0f) | value);
        }

        public Group Gr {
            get => (Group) ((Val >> 4) & 0x07);
            set => Val = (byte)((Val & ~0x70) | ((byte)value << 4));
        }

        public bool Akadora {
            get => (Val & 0x80) != 0;
            set {
                if (value) Val |= 0x80;
                else Val &= 0x7f;
            }
        }

        public bool IsValid {
            get {
                if (Gr == Group.Invalid)
                    return false;
                if (Num < 1 || Num > 9)
                    return false;
                if (Gr == Group.Z && Num > 7)
                    return false;
                return true;
            }
        }

        public bool IsEmpty => this == Empty;

        public Hai(byte val = 0) {
            Val = val;
        }

        public Hai(string str) {
            Val = 0;
            string original = str;
            if (str.StartsWith("!")) {
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
            Gr = str[1].ToGroup();
            if (!IsValid) {
                ThrowInvalidArgument(original);
            }
        }

        public override string ToString() {
            var builder = new StringBuilder();
            if (Akadora)
                builder.Append('!');
            builder.Append(Num);
            builder.Append(Gr.ToChar());
            return builder.ToString();
        }

        public override bool Equals(object obj) {
            if (obj is Hai rhs) {
                return this == rhs;
            }
            return false;
        }

        public override int GetHashCode() {
            return Val;
        }

        public int CompareTo(Hai other) {
            int grcmp = Gr.CompareTo(other.Gr);
            if (grcmp != 0)
                return grcmp;
            int numcmp = Num.CompareTo(other.Num);
            if (numcmp != 0)
                return numcmp;
            return Akadora.CompareTo(other.Akadora);
        }

        public static implicit operator byte(Hai t) => t.Val;
        private static void ThrowInvalidArgument(string arg) {
            throw new ArgumentException("Invalid cast to hai: " + arg);
        }

        public static bool operator == (Hai lhs, Hai rhs) {
            return lhs.Val == rhs.Val;
        }
        public static bool operator != (Hai lhs, Hai rhs) {
            return lhs.Val != rhs.Val;
        }
    }

    /// <summary>
    /// Stores a list of hais.
    /// </summary>
    public class Hais : List<Hai> {
        public Hais(IEnumerable<Hai> tiles) : base(tiles) { }
        public Hais(string tiles = "") : base() {
            bool isDora = false;
            foreach (char c in tiles) {
                if (c > '0' && c <= '9') {
                    byte num = (byte)(c - '0');
                    Add(new Hai {
                        Num = num,
                        Akadora = isDora,
                    });
                    isDora = false;
                    continue;
                }
                if (isDora) {
                    ThrowInvalidArgument(tiles);
                }
                if (c == '!') {
                    isDora = true;
                    continue;
                }
                var g = c.ToGroup();
                if (g == Group.Invalid) {
                    ThrowInvalidArgument(tiles);
                }
                for (int i = Count - 1; i >= 0; i--) {
                    var hai = this[i];
                    if (hai.Gr != Group.Invalid) {
                        break;
                    }
                    hai.Gr = g;
                    this[i] = hai;
                }
            }
            if (this.Any(hai => !hai.IsValid)) {
                ThrowInvalidArgument(tiles);
            }
        }

        public void Remove(IEnumerable<Hai> hais) {
            foreach (var hai in hais) {
                Remove(hai);
            }
        }

        public override string ToString() {
            var builder = new StringBuilder();
            char group = '\0';
            foreach (var hai in this) {
                string str = hai.ToString();
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

        public static Hais All {
            get {
                var ret = new Hais();
                for (var g = Group.M; g <= Group.Z; g++) {
                    int maxTile = g == Group.Z ? 7 : 9;
                    for (int i = 1; i <= maxTile; i++) {
                        for (int j = 0; j < 4; j++) {
                            ret.Add(new Hai {
                                Gr = g,
                                Num = (byte)i,
                                Akadora = maxTile == 9 && i == 5 && j == 0
                            });
                        }
                    }
                }
                return ret;
            }
        }
    }
}
