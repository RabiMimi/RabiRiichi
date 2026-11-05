using System.Diagnostics;

namespace RabiRiichi.Benchmarks {
  /// <summary>Search effort reported by a scenario; zero when it ran no wildcard search.</summary>
  internal readonly record struct BenchWork(long Targets, long Interpretations) {
    public static readonly BenchWork None = new(0, 0);
  }

  /// <param name="Op">Runs case <c>i</c>. Cases wrap, so i may exceed the fixture size.</param>
  /// <param name="MinOps">Measure at least this many operations even past the budget.</param>
  internal sealed record Scenario(string Name, Func<int, BenchWork> Op, int MinOps = 3, int MaxOps = 20000);

  internal sealed record Measurement(string Name, int Ops, double MeanUs, double P50Us, double P95Us, double MaxUs,
      double AllocKb, double Targets, double Interpretations);

  internal static class BenchmarkRunner {
    /// <summary>
    /// Scenario costs span six orders of magnitude and are heavy-tailed, so each
    /// operation is timed on its own and the worst case is reported with the mean.
    /// </summary>
    internal static Measurement Run(Scenario scenario, int budgetMs) {
      // Warm up so tiering has happened; expensive scenarios stop after one call.
      var warmup = Stopwatch.StartNew();
      int warmupOps = 0;
      do scenario.Op(warmupOps++); while (warmup.ElapsedMilliseconds < budgetMs / 4 && warmupOps < scenario.MaxOps);

      GC.Collect();
      GC.WaitForPendingFinalizers();
      var samples = new List<double>();
      long targets = 0, interpretations = 0;
      long budgetTicks = (long)(budgetMs / 1000.0 * Stopwatch.Frequency);
      long allocated = GC.GetTotalAllocatedBytes(true);
      long start = Stopwatch.GetTimestamp();
      while (samples.Count < scenario.MaxOps
          && (samples.Count < scenario.MinOps || Stopwatch.GetTimestamp() - start < budgetTicks)) {
        long before = Stopwatch.GetTimestamp();
        var work = scenario.Op(samples.Count);
        samples.Add((Stopwatch.GetTimestamp() - before) * 1e6 / Stopwatch.Frequency);
        targets += work.Targets; interpretations += work.Interpretations;
      }
      allocated = GC.GetTotalAllocatedBytes(true) - allocated;

      samples.Sort();
      int n = samples.Count;
      return new(scenario.Name, n, samples.Sum() / n, samples[n / 2], samples[Math.Min(n - 1, n * 95 / 100)], samples[n - 1],
          allocated / 1024.0 / n, (double)targets / n, (double)interpretations / n);
    }

    internal static string FormatTime(double us) => us switch {
      >= 1_000_000 => $"{us / 1_000_000:F2} s",
      >= 1_000 => $"{us / 1_000:F2} ms",
      _ => $"{us:F1} us",
    };
  }
}