using System.Linq;

namespace RabiRiichi.Riichi {
    public static class Extensions {
        public static TileSuit ToGroup(this char c) {
            c = char.ToLower(c);
            return c switch {
                'm' => TileSuit.M,
                'p' => TileSuit.P,
                's' => TileSuit.S,
                'z' => TileSuit.Z,
                _ => TileSuit.Invalid,
            };
        }

        public static char ToChar(this TileSuit g) {
            return g switch {
                TileSuit.M => 'm',
                TileSuit.P => 'p',
                TileSuit.S => 's',
                TileSuit.Z => 'z',
                _ => '_',
            };
        }

        private static int GetZUnicode(int num) {
            if (num > 4)
                num = 12 - num;
            return 0x1EFFF + num;
        }

        public static string ToUnicode(this Tile tile) {
            var ret = tile.Akadora ? "✨" : "";
            ret += tile.Suit switch {
                TileSuit.M => char.ConvertFromUtf32(0x1F006 + tile.Num),
                TileSuit.P => char.ConvertFromUtf32(0x1F018 + tile.Num),
                TileSuit.S => char.ConvertFromUtf32(0x1F00F + tile.Num),
                TileSuit.Z => char.ConvertFromUtf32(GetZUnicode(tile.Num)),
                _ => "🀫",
            };
            return ret;
        }

        public static string ToUnicode(this Tiles tiles) {
            return string.Concat(tiles.Select(tile => ToUnicode(tile)));
        }
    }
}
