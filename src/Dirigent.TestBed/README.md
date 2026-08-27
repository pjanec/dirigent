# Dirigent test harness

Integration testing for Dirigent's local *and* distributed behaviour. The full design is in the
"Rehearsal Room" document; this file is the working state: how to use what exists, the rules that
keep it trustworthy, and exactly what comes next.

## Status

| Piece | State |
| --- | --- |
| `Dirigent.TestApp` — controllable stand-in application | done |
| `Dirigent.TestBed` — tier-1 harness (master + agents + operator in one process) | done |
| `Dirigent.IntegrationTests` — tier-1 tests | 26 tests |
| Isolation seams (`--agentStatusFolder`, `--downloadFolder`) | done |
| Scenario model + renderers + round-trip guard | done, 8 tests in `Dirigent.CommonTests` |
| Log-download test at tier 1 | done, 3 tests |
| Files over the CLI/REST surface | done, 6 tests |
| PowerShell tier-2 driver | **next** |
| Breadth at tier 1 (plans, detectors, restarts, env vars, reconnect) | not started |
| Tier 3 on the two VMs | not started |

Relevant commits: `43855b1` (harness), `11d9732` (seams + scenarios), `fd7d90c` (docs).

## Running

```
dotnet test src/Dirigent.IntegrationTests   # tier 1, ~17 s
dotnet test src/Dirigent.CommonTests        # unit tests incl. the scenario renderer
```

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
- `HttpPort` is 0 in every bed: the web server binds `http://*:port`, which needs a URL ACL.
- The master always binds `MasterPort` and `CliPort`, so both are allocated free per bed.

---

# Way forward

## Done: the log download at tier 1

`LogDownloadTests` covers what the harness was built for: a package collecting the recent logs of
three applications on two machines arrives as one archive, laid out
`m1/log/Recent logs/camera/app.log`; the nine-day-old seeded file is filtered out by `MaxSeconds`;
`Args="perMachine"` yields one archive per machine instead; a single application's node takes only
its own; the staging folder is removed and the operator is notified.

The blocking question - a tier-1 download folder under `%TEMP%` that no share covers - was
answered the way the product wanted anyway: **each slave is handed the destination as both a local
and a UNC path and uses the local one when it owns the folder.** A slave on the requestor's own
machine no longer copies its archive through `\\ip\share\...` to its own disk, and a machine
that cannot reach the folder is now reported in the final message instead of failing the whole
download. Two real bugs surfaced on the way, both in code that had never run in the field:

- `<FileRef MachineId="*">` was dispatched for resolution to a machine literally named `*`, so a
  package gathering files from every machine could never resolve. Guarded by
  `ResolveFolderTests.WildcardReferenceIsLookedUpLocallyTest`.
- the global (machine-less) files were assigned to the first machine in the list even when no
  slave was started for it.

## Done: the remote-control surface for files

The plan here was three new CLI commands. It turned out none was needed. `StartScript` plus
`GetScriptState` already carry everything; what was missing was the ability to *name* a node from
outside, since only a GUI can hand a script a resolved node tree. So each VFS script now takes a
`VfsNodeSelector` - `{Node:{Id:"logs.all"}}`, the same id/machine/app triplet a `<FileRef>` uses -
and resolves it itself. `BuiltIns/ListVfsNodes.cs` is new, because nothing listed the
declarations; `DownloadZipped.TResult`, which was an empty class, now carries the archive paths,
the machines that took part and the per-machine errors.

Script arguments are always JSON deserialisable into the script's argument class - no script
parses a plain string. Documented in `docs/Scripts.md`, exercised by `CliSurfaceTests`.

One product bug fell out of it: the CLI parsed the request id greedily up to the **last** `]` in
the line, so any request whose arguments contained a JSON array had its tail chopped off and
parsed as another command. `JsonArgumentsMayContainArrays` fails on the old regex and passes on
the new one.

`CliSession` (in the harness) wraps `CommandLineClient` for tests, and is the shape the PowerShell
verbs should take: send a request, read the answer, throw on `ERROR:`.

## Next: the tier-2 driver

Tier 2 is needed periodically and must be runnable by hand, not only by a test runner.

1. **`Dirigent.TestBed.Gen`** - a console tool rendering a named scenario to a folder
   (`--scenario LoggingApps --out <dir>`), so PowerShell consumes the same scenario model instead
   of owning a second copy. Tier 3 uses it too, before robocopy.
2. **PowerShell module + Pester**: `Start-DirigentWorld`, `Invoke-DirigentCli`,
   `Wait-DirigentCondition`, `Stop-DirigentWorld`, and `Invoke-DirigentTests.ps1` with
   `-KeepAlive -WithGui` so it doubles as a curated world to poke at. This replaces the
   `run_m1_gui_master.bat` / `run_m2_con.bat` workflow. `Invoke-DirigentCli` mirrors `CliSession`.

Keep tier 2 small and deliberate: the hosting model and startup, agent kill-and-recover against
the status file, both remote-control surfaces answering, and one end-to-end download.

REST needs no work of its own - `CmdApiController` exposes the whole command repository through
`POST /cli`, so anything reachable from the CLI is reachable over HTTP.

## Then: breadth at tier 1

Where the harness starts paying for itself. Each of these has a test-app switch already:
plans and dependencies, init detectors (`ReadyAfter`), restart on crash (`ExitsAfter`), soft kill
and kill tree (`IgnoresClose`, `SpawnsChildren`), env var propagation (`PrintsEnvironment`),
config reload, client reconnect. The additive `Master`-takes-a-`SharedConfig` seam belongs here,
to drop temp-file churn from the fast tier.

## Last: tier 3 on the VMs

Reuse the generator and the PowerShell verbs against the existing `config/VM` scaffolding
(`set-vm-IPs.ps1`, `inst_m*.bat`) as a nightly or pre-release gate. Only this tier covers real SMB
with credentials, distinct user profiles, a machine going offline mid-download, and reboot.

## Standing constraints

- **Compatibility first outside the VFS.** Dirigent is widely deployed. Prefer new optional
  settings, new commands and new overloads whose defaults reproduce existing behaviour exactly.
  Avoid changing wire messages, existing command responses, or the meaning of existing fields —
  avoid, not "do carefully". The file/package (VFS) subsystem is the exception: never used in the
  field, so its shape may change freely, though its config attributes stay backward compatible
  because example configs circulate.
- **Two agents can share an address.** In a tier-1 bed every machine is `127.0.0.1`, so any logic
  identifying a machine by address is ambiguous there. The download reads the operator's machine
  from the client name prefix (`{machineId}_gui_{guid}`) for this reason; the same pattern may
  exist elsewhere and is worth a grep rather than an assumption.
