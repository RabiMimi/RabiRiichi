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

        public static string ToUnicode(this Hai hai) {
            var ret = hai.Gr switch {
                Group.M => char.ConvertFromUtf32(0x1F006 + hai.Num),
                Group.P => char.ConvertFromUtf32(0x1F018 + hai.Num),
                Group.S => char.ConvertFromUtf32(0x1F00F + hai.Num),
                Group.Z => char.ConvertFromUtf32(GetZUnicode(hai.Num)),
                _ => "🀫",
            };
            if (hai.Akadora) {
                ret += "✨";
            }
            return ret;
        }

        public static string ToUnicode(this Hais hais) {
            return string.Concat(hais.Select(hai => ToUnicode(hai)));
        }
    }
}
