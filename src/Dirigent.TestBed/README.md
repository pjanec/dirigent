# Dirigent test harness

Integration testing for Dirigent's local *and* distributed behaviour. The full design is in the
"Rehearsal Room" document; this file is the working state: how to use what exists, the rules that
keep it trustworthy, and exactly what comes next.

## Status

| Piece | State |
| --- | --- |
| `Dirigent.TestApp` — controllable stand-in application | done |
| `Dirigent.TestBed` — tier-1 harness (master + agents + operator in one process) | done |
| `Dirigent.IntegrationTests` — tier-1 tests | 11 tests |
| Isolation seams (`--agentStatusFolder`, `--downloadFolder`) | done |
| Scenario model + renderers + round-trip guard | done, 8 tests in `Dirigent.CommonTests` |
| Log-download test at tier 1 | **next** |
| CLI/REST VFS commands | not started |
| PowerShell tier-2 driver | not started |
| Breadth at tier 1 (plans, detectors, restarts, env vars, reconnect) | not started |
| Tier 3 on the two VMs | not started |

Relevant commits: `43855b1` (harness), `11d9732` (seams + scenarios).

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
- `Isolation` — free ports, temp root. Machine ids are used verbatim.
- `Diagnostics` — process-global log4net capture, cleared per test via `Diagnostics.ClearLog()`.

## Known behaviour

- If a test fails while a remote script is still pending, teardown can take up to 5 s while the
  pump is joined, and prints `pump thread did not stop within 5 s`. Bounded and harmless.
- `HttpPort` is 0 in every bed: the web server binds `http://*:port`, which needs a URL ACL.
- The master always binds `MasterPort` and `CliPort`, so both are allocated free per bed.

---

# Way forward

## Next: the log download at tier 1

The test that motivated the whole harness. `Scenario.LoggingApps()` already builds the world.

**The one thing to decide first.** The download resolves the destination folder with
`forceUNC: true`, because the folder must be reachable from every participating machine. In a
tier-1 bed the download folder is under `%TEMP%`, which no Windows share covers, so `MakeUNC`
will throw. Three ways out, in order of preference:

1. **Let each slave choose** (recommended, and a genuine product improvement): pass the
   destination as both a local path and a UNC path, and have each slave use the local one when it
   is on the machine that owns the folder. Today even a local slave copies through
   `\\ip\share\...`, which is pointless work in production too. VFS-only, so class A.
2. Create and remove a temporary Windows share in fixture setup — needs administrator, so the
   test must skip loudly when it cannot.
3. Point the bed's download folder somewhere already shared (`C$`) — still needs an elevated token.

**Then the test.** Start the plan, wait for the apps to run, trigger the download, and assert:
one archive; a folder per machine; a per-app subfolder inside; the nine-day-old seeded file
absent because `MaxSeconds` is two days; the operator notified; the staging folder gone.

**Operator support needed.** `Operator.ResolveAsync` exists. Add a `Download` that mirrors what
the GUI does: resolve the package node, then run `BuiltIns/DownloadZipped.cs` with
`ScriptActionArgs { VfsNode = resolved }` via `ReflStates.ScriptReg.RunScriptAsync<,>` on the
master, and wait for the script to finish (`GetScriptState`, or the notification).

## Then: CLI and REST commands, and the tier-2 driver

Tier 2 is needed periodically and must be runnable by hand, not only by a test runner.

1. **Three commands**, registered in `MyCommandRepo` with implementations alongside
   `DirigentControlCommands.cs`. Additive only — new names, nothing existing changed:
   - `GetAllVfsNodes` → json
   - `ResolveVfsNode <idOrGuid>` → json, so "is the file really there" is answerable remotely
   - `DownloadVfsNode <scriptGuid> <idOrGuid> [perMachine]` → `ACK`, then poll
     `GetScriptState <scriptGuid>`, exactly like `StartScript` already works.
   - Supporting change: `DownloadZipped` should publish the resulting archive path through
     `SetStatus( data: ... )` so it arrives in `ScriptState.Data` instead of only a message box.
     That is also what a crash-triggered log collection would use.
2. **`Dirigent.TestBed.Gen`** — a console tool rendering a named scenario to a folder
   (`--scenario LoggingApps --out <dir>`), so PowerShell consumes the same scenario model instead
   of owning a second copy. Tier 3 uses it too, before robocopy.
3. **PowerShell module + Pester**: `Start-DirigentWorld`, `Invoke-DirigentCli`,
   `Wait-DirigentCondition`, `Stop-DirigentWorld`, and `Invoke-DirigentTests.ps1` with
   `-KeepAlive -WithGui` so it doubles as a curated world to poke at. This replaces the
   `run_m1_gui_master.bat` / `run_m2_con.bat` workflow.

Keep tier 2 small and deliberate: the hosting model and startup, agent kill-and-recover against
the status file, both remote-control surfaces answering, and one end-to-end download.

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
