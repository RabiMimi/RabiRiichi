using System;
using System.Linq;
using System.Text;

namespace RabiRiichi.Riichi {
    public static class Extensions {
        public static Group ToGroup(this char c) {
            c = char.ToLower(c);
            return c switch {
                'm' => Group.M,
                'p' => Group.P,
                's' => Group.S,
                'z' => Group.Z,
                _ => Group.Invalid,
            };
        }

        public static char ToChar(this Group g) {
            return g switch {
                Group.M => 'm',
                Group.P => 'p',
                Group.S => 's',
                Group.Z => 'z',
                _ => '_',
            };
        }

        private static int GetZUnicode(int num) {
            if (num > 4) num = 12 - num;
            return 0x1EFFF + num;
        }

        public static string ToUnicode(this Tile tile) {
            var ret = tile.Akadora ? "✨" : "";
            ret += tile.Gr switch {
                Group.M => char.ConvertFromUtf32(0x1F006 + tile.Num),
                Group.P => char.ConvertFromUtf32(0x1F018 + tile.Num),
                Group.S => char.ConvertFromUtf32(0x1F00F + tile.Num),
                Group.Z => char.ConvertFromUtf32(GetZUnicode(tile.Num)),
                _ => "🀫",
            };
            return ret;
        }

        public static string ToUnicode(this Tiles tiles) {
            return string.Concat(tiles.Select(tile => ToUnicode(tile)));
        }
    }
}
