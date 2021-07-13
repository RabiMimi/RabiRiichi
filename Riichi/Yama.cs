using System.Collections.Generic;

namespace RabiRiichi.Riichi {
    public class Yama {
        public Hais drawn;
        public Hais remaining;

        public Yama(string tiles = "") {
            drawn = new Hais(tiles);
            remaining = Hais.All;
            remaining.Remove(drawn);
        }

        public bool Draw(Hai hai) {
            if (!remaining.Contains(hai)) {
                return false;
            }
            remaining.Remove(hai);
            drawn.Add(hai);
            return true;
        }

        public bool Draw(IEnumerable<Hai> hais) {
            foreach (var hai in hais) {
                if (!Draw(hai))
                    return false;
            }
            return true;
        }
    }
}
