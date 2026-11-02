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

A full run takes several minutes. `dev/local/` is ignored by Git.

## Scenarios

- `natural/*`: the default wall, which uses the natural-only code paths.
- `wild<W>/*`: a wildcard-enabled table with `W` wildcards in the hand.
  `-koyaku` turns on every optional yaku and `-open` adds a called pon.
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

`targets` and `evals` are `WildcardScoreSearch` effort: complete hand targets
visited, and yaku evaluations of those targets. They are zero when no search ran.
Costs are heavy-tailed, so look at `p95` and `max` as well as the mean. Each
scenario runs at least three operations, so the slowest ones can overrun the budget.
