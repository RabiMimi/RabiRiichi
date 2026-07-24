using System.IO;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// Helpers for null-safe, atomic (temp file + rename) disk writes so a crash
  /// mid-write never leaves a partial/corrupt file. Used by every Arena store.
  /// </summary>
  public static class AtomicFile {
    /// <summary>
    /// Writes <paramref name="contents"/> to <paramref name="path"/> atomically:
    /// the bytes are first written to a sibling temp file which is then renamed
    /// over the destination. Creates the parent directory if needed.
    /// </summary>
    public static void WriteAllText(string path, string contents) {
      if (string.IsNullOrEmpty(path)) {
        throw new ArgumentException("Path must not be empty", nameof(path));
      }
      contents ??= string.Empty;
      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(dir)) {
        Directory.CreateDirectory(dir);
      }
      var tmp = path + ".tmp-" + Guid.NewGuid().ToString("N");
      try {
        File.WriteAllText(tmp, contents);
        // File.Move with overwrite is atomic on the same volume.
        File.Move(tmp, path, overwrite: true);
      } finally {
        if (File.Exists(tmp)) {
          try {
            File.Delete(tmp);
          } catch {
            // Best-effort cleanup of the temp file.
          }
        }
      }
    }
  }
}
