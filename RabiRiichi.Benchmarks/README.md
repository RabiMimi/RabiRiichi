# Benchmarks

Manual performance benchmarks for the engine. This is **not** a test: nothing
asserts a time limit, the test project does not reference it, and CI does not
build or run it. Run it yourself when changing scoring, shanten or the bots.

```bash
dotnet run -c Release --project RabiRiichi.Benchmarks
```

Useful options (`--help` lists them all):

```bash
# Only the wildcard tables, spending 5 seconds on each scenario
dotnet run -c Release --project RabiRiichi.Benchmarks -- --filter wild --budget-ms 5000

# Compare a change against the commit before it
dotnet run -c Release --project RabiRiichi.Benchmarks -- --json dev/local/before.json
dotnet run -c Release --project RabiRiichi.Benchmarks -- --baseline dev/local/before.json
```

For a machine with the .NET 10 runtime but no .NET 9 runtime, build normally and
run the compiled benchmark with major-version roll-forward (PowerShell):

```powershell
dotnet build -c Release RabiRiichi.Benchmarks/RabiRiichi.Benchmarks.csproj
dotnet --roll-forward Major RabiRiichi.Benchmarks/bin/Release/net9.0/RabiRiichi.Benchmarks.dll --filter aotenjou-koyaku/hard --cases 3 --budget-ms 500
```

Use `--list --filter aotenjou-koyaku` to choose workloads; `--filter` accepts
comma-separated substrings. Run matching `--seed` and `--cases` values when
comparing versions. The seed controls the table's shuffled wall as well as the
fixture generator. A full run takes several minutes. `dev/local/` is ignored by Git.

## Scenarios

- `natural/*`: the default wall, which uses the natural-only code paths.
- `wild<W>/*`: a wildcard-enabled table with `W` wildcards in the hand.
  `-koyaku` turns on every optional yaku and `-open` adds a called pon.
- `aotenjou-koyaku/wild<W>/*`: uncapped scoring with all optional yaku,
  for 8, 10, 12, and 13 generated wildcards.
- `aotenjou-koyaku/hard/*`: fixed high-wildcard preview and discard holdings;
  these provide stable comparisons independent of the generated fixtures.
- `selfplay/wall<N>`: a whole east-only game between four rule-based bots, with
  `N` wildcards added to the wall. This shows what the numbers above add up to.

| Operation | Runs when |
| --- | --- |
| `shanten` | Waits and bot hand evaluation |
| `score` | A win is checked or paid |
| `previews` | Waits and values for one tenpai holding |
| `discard` | Every draw: `previews` for each discard that stays tenpai |
| `draw` | Every draw: all the actions offered, including `discard` and riichi |

`discard/near-win` starts close to a win (the worst case); `discard/dealt` starts
from a random hand (most turns).

## Reading the output

`targets`, `evals`, `nodes`, and `bounds` are mean `WildcardScoreSearch` effort:
complete targets visited, yaku evaluations, search nodes, and structural bound
calculations. Reusing a structural bound for another win context does not count
as another calculation.
Score and preview scenarios report their search counters. Discard scenarios
report counters when a shared discard cache is active; `cache hits` counts reused
candidate evaluations. Sharing is enabled for at least eight wildcards and four
distinct discard values, where the larger searches repay its setup cost. Zero
counters on other discard scenarios mean diagnostics were unavailable, not that
the search did no work. Older baseline JSON files remain readable; new counters
default to zero when absent.
Costs are heavy-tailed, so look at `p95` and `max` as well as the mean. Each
scenario runs at least three operations, so the slowest ones can overrun the budget.
