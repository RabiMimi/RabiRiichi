using System.Collections.Generic;

namespace RabiRiichi.Utils {
  public class RandIDPool(RabiRand rand, int maxId = int.MaxValue) {
    private readonly RabiRand rand = rand;
    private readonly int maxId = maxId;
    private readonly HashSet<int> usedIds = [];

    public void Reset() {
      usedIds.Clear();
    }

    public int GetID() {
      int id = rand.Next(maxId);
      while (usedIds.Contains(id)) {
        id = rand.Next(maxId);
      }
      usedIds.Add(id);
      return id;
    }
  }
}