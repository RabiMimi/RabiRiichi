using System.Text.Json;

namespace RabiRiichi.Benchmarks {
  internal static class BenchmarkCli {
    private const string Usage = """
        Manual engine benchmarks. Not a test: nothing here asserts a time limit.

          dotnet run -c Release --project RabiRiichi.Benchmarks -- [options]

          --list               Print scenario names and exit
          --filter a,b         Only run scenarios whose name contains a or b
          --budget-ms N        Time to spend on each scenario (default 2000)
          --cases N            Fixture hands per scenario (default 50)
          --seed N             Fixture and wall seed (default 12345)
          --json PATH          Write results as JSON
          --baseline PATH      Show mean time relative to an earlier --json file
        """;

    internal static int Run(string[] args) {
      string filter = null, json = null, baseline = null;
      int budgetMs = 2000, cases = 50, seed = 12345;
      bool list = false;
      for (int i = 0; i < args.Length; i++) {
        string Value() => ++i < args.Length ? args[i] : throw new ArgumentException($"{args[i - 1]} needs a value");
        switch (args[i]) {
          case "--list": list = true; break;
          case "--filter": filter = Value(); break;
          case "--budget-ms": budgetMs = int.Parse(Value()); break;
          case "--cases": cases = int.Parse(Value()); break;
          case "--seed": seed = int.Parse(Value()); break;
          case "--json": json = Value(); break;
          case "--baseline": baseline = Value(); break;
          default: Console.WriteLine(Usage); return args[i] is "--help" or "-h" ? 0 : 1;
        }
      }
#if DEBUG
      Console.WriteLine("WARNING: Debug build; numbers are not meaningful. Use -c Release.");
#endif
      var filters = filter?.Split(',', StringSplitOptions.RemoveEmptyEntries);
      var scenarios = Scenarios.All(seed, cases)
          .Where(s => filters == null || filters.Any(f => s.Name.Contains(f, StringComparison.OrdinalIgnoreCase))).ToList();
      if (list) {
        scenarios.ForEach(s => Console.WriteLine(s.Name));
        return 0;
      }

      var previous = baseline == null ? []
          : JsonSerializer.Deserialize<List<Measurement>>(File.ReadAllText(baseline)).ToDictionary(m => m.Name);
      Console.WriteLine($"{"scenario",-34} {"mean",11} {"p50",11} {"p95",11} {"max",11} {"KB/op",10} {"ops",6} {"targets",11} {"evals",11}{(baseline == null ? "" : $" {"vs base",9}")}");
      var results = new List<Measurement>();
      foreach (var scenario in scenarios) {
        var m = BenchmarkRunner.Run(scenario, budgetMs);
        results.Add(m);
        string ratio = previous.TryGetValue(m.Name, out var old) ? $" {m.MeanUs / old.MeanUs,8:F2}x" : "";
        Console.WriteLine($"{m.Name,-34} {BenchmarkRunner.FormatTime(m.MeanUs),11} {BenchmarkRunner.FormatTime(m.P50Us),11} "
            + $"{BenchmarkRunner.FormatTime(m.P95Us),11} {BenchmarkRunner.FormatTime(m.MaxUs),11} {m.AllocKb,10:F1} {m.Ops,6} "
            + $"{m.Targets,11:F0} {m.Interpretations,11:F0}{ratio}");
      }
      if (json != null) File.WriteAllText(json, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
      return 0;
    }
  }
}