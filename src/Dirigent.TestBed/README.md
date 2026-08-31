# Dirigent test harness

Integration testing for Dirigent's local *and* distributed behaviour. This file is the working
guide: how to run what exists, how to write a test, and the rules that keep it trustworthy. The
design and the roadmap are in [`docs/TestHarness.md`](../../docs/TestHarness.md).

## Status

| Piece | State |
| --- | --- |
| `Dirigent.TestApp` — controllable stand-in application | done |
| `Dirigent.TestBed` — tier-1 harness (master + agents + operator in one process) | done |
| `Dirigent.IntegrationTests` — tier-1 tests | 63 tests |
| Isolation seams (`--agentStatusFolder`, `--downloadFolder`) | done |
| Scenario model + renderers + round-trip guard | done, 8 tests in `Dirigent.CommonTests` |
| Log-download tests at tier 1 | done, 6 tests |
| Files over the CLI/REST surface | done, 6 tests |
| Tier 2: `Dirigent.TestBed.Gen` + PowerShell driver | done, 8 tests |
| Breadth at tier 1 (plans, detectors, restarts, kills, env vars, reload, reconnect) | done |
| Tier 3 on the two VMs | deferred - not wanted yet |
| Merged with branch 3.1, on .NET 8, one harness only | done |
| Streamed archives, size-budget skips, `TailBytes` | done, 13 tests in `Dirigent.CommonTests` |
| Progress and cancellation of long operations | done, see [`docs/ScriptProgress.md`](../../docs/ScriptProgress.md) |
| A note from the operator, kept in the archive | done, 4 tests |
| Clear / Mark / Unmark, and collecting one run | done, 13 tests in `Dirigent.CommonTests` + 10 at tier 1 |

Relevant commits: `43855b1` (harness), `11d9732` (seams + scenarios), `fd7d90c` (docs).

## Running

```
dotnet test src/Dirigent.IntegrationTests   # tier 1, ~80 s
dotnet test src/Dirigent.CommonTests        # unit tests incl. the scenario renderer

src\Dirigent.TestBed.PowerShell\Invoke-DirigentTests.ps1            # tier 2, ~31 s
src\Dirigent.TestBed.PowerShell\Invoke-DirigentTests.ps1 -KeepAlive -WithGui
```

Tier 2 has its own README in `src/Dirigent.TestBed.PowerShell`.

Every run writes `TestResults/last-run.trx` in the test project, whether or not a logger was asked
for on the command line. The test that needs it is the one failing once in fifty runs, and by then
the run is over: the file names it.

## Writing a test

```csharp
var scenario = Scenario.TwoMachines()
    .App( "m1.camera", a => a.LongRunning().WritesLog().WithLogNode() )
    .Package( "logs.all", "Logs/All apps", p => p.RefAll( "log" ) )
    .Seed( "m1.camera", "old.log", ageDays: 9 );

using var bed = await TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

await bed.Operator.StartAppAsync( bed.App( "m1", "camera" ) );
await bed.WaitUntilAsync(
    async () => (await bed.Operator.GetAppStateAsync( bed.App( "m1", "camera" ) ))?.Running ?? false,
    TimeSpan.FromSeconds( 20 ), "the camera reports running" );
```

`StartAsync` returns once the master, every agent and the operator are connected.

## Rules

These are not style preferences; each one is load-bearing.

**No `Sleep` in test code, ever.** There is no virtual time in Dirigent (`Clock` is a wall-clock
singleton and the components pace themselves with `Stopwatch`), so a fixed sleep is a guess that
will eventually fail on a loaded machine. The only wait is
`WaitUntilAsync( condition, timeout, because )`, which on expiry dumps client states, app
definitions, app states, notifications and the tail of the log4net stream.

A *negative* property - "it was not restarted" - has no condition to wait for, so it is checked by
observing for a bounded window instead. That is the one legitimate use of a delay, and it cannot be
flaky: it only fails if the thing being ruled out actually happens.

**Read state only through the operator.** The pump thread owns every `Tick()`. `Operator` marshals
each call into the tick through `SynchronousOpProcessor`, so a read never observes a half-applied
tick. Touching `ReflectedStateRepo` or a client directly from the test thread is what produces
"collection was modified" flakiness.

**Anything that awaits a script goes through `Operator.OffPump`.** A script result is delivered
from inside the pump's tick, and `ReflectedScriptRegistry` completes its task right there, on the
pump thread. Awaiting such a task directly moves the rest of the test body onto the pump thread,
where the next `Dispose` waits five seconds for a thread that is itself and the survivor scan
times out, leaving processes and temp folders behind. `ResolveAsync` and `DownloadAsync` already
do this; a new operator call that awaits a script must too. `TestBed.Dispose` says so out loud if
it ever happens again. (The product is right as it is: in a real GUI the tick thread is the UI
thread, which is exactly where a continuation belongs.)

**Applications stay minimized.** `WindowStyleSpec.Minimized` is the scenario default so a run does
not interrupt whoever is at the keyboard. Override it only in a test about the window style itself.

**Check for leaks, do not assume.** A green run can still leave processes behind — the first
version of this harness did. After changing teardown, verify:

```powershell
Get-Process -Name Dirigent.TestApp -ErrorAction SilentlyContinue    # expect nothing
Get-ChildItem (Join-Path $env:TEMP DirigentTestBed) -Directory       # expect nothing
```

**Scenarios, not config strings.** `TestBedOptions.SharedConfigXml` still exists for tests that are
about the config text itself; everything else uses a `Scenario` so the same description can later
render for real processes and for VMs.

## How it fits together

- `TestBed` — owns the temp world, the ports, the pump thread, the components, and teardown.
  Kills applications the run left behind, checking each process really is the test app.
- `Operator` — a GUI client with no GUI: commands, observations, VFS resolution, and the
  `UserNotificationMessage`s a real operator would see (`bed.Operator.Notifications`).
- `Scenarios/` — `ScenarioSpec` is plain data; `Scenario` is the fluent builder with presets and
  app mixins; `SharedConfigRenderer` renders XML with `XElement` (so paths and masks escape);
  `WorldSeeder` creates the folders and back-dated files; `RenderContext` holds the run's paths.
- `CliSession` — a text-command session with the master over a real socket, for tests that drive
  the remote-control surface rather than the in-process client.
- `Isolation` — free ports, temp root. Machine ids are used verbatim.
- `Diagnostics` — process-global log4net capture, cleared per test via `Diagnostics.ClearLog()`.
- `../Dirigent.TestBed.Gen` — renders a scenario preset to a folder for tiers 2 and 3.
- `../Dirigent.TestBed.PowerShell` — the tier-2 driver and its tests.

## Known behaviour

- If a test fails while a remote script is still pending, teardown can take up to 5 s while the
  pump is joined, and prints `pump thread did not stop within 5 s`. Bounded and harmless.
- `VfsNodeDef.Guid` defaults to `Guid.Empty` (`new Guid()` generates nothing). The config reader
  hands out real guids, so this only bites hand-built nodes in tests: two of them share a guid,
  the resolver takes that for a circular reference and returns null. Set `Guid = Guid.NewGuid()`
  when building nodes in test code.
- A scenario must not declare a `<Share>` unless the test is about UNC construction. With a share
  in the config the download sends the other machines through `\\127.0.0.1\C$`, which needs an
  elevated token; with none, every "machine" writes to the folder directly, which is what a
  tier-1 bed actually is.
- A **plan's `<App>` is a complete application definition**, not a reference to the standalone one.
  Rendering only the id leaves Dirigent trying to launch the startup folder, which surfaces as
  "Access is denied". `Plan( name, p => p.App( ... ) )` renders the whole application and lays the
  plan's own attributes over it - which is also where dependencies and init conditions belong, as
  the loader rejects a dependency it cannot resolve among the standalone definitions.
- **`ReloadSharedConfig` sleeps three seconds inside the master's tick.** Nothing is answered while
  it runs, so a test around a reload needs a timeout comfortably above that.
- **A graceful agent stop deletes its status file**, by design, so nothing is left to adopt.
  `StopAgent( machine, crash: true )` keeps the file, which is the only way to exercise post-crash
  adoption in-process.
- `HttpPort` is 0 in every bed, which switches the web server off - no test needs it in-process.
  Note for anything outside the bed: on the *command line* `--httpPort 0` does **not** switch it
  off, it falls back to 8877; `-1` does. (No URL ACL is needed either way - EmbedIO listens on its
  own socket rather than through `HttpListener`, as the tier-2 REST test demonstrates.)
- The master always binds `MasterPort` and `CliPort`, so both are allocated free per bed.

---

# Where the rest of it is written down

The design, the principles behind these rules, what the harness has found, and the roadmap are in
[`docs/TestHarness.md`](../../docs/TestHarness.md) - one place, so the plan cannot say two things.
Tier 2 has its own practical guide in
[`../Dirigent.TestBed.PowerShell/README.md`](../Dirigent.TestBed.PowerShell/README.md).
